using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Newtonsoft.Json;
using Obsidian.Features.SegWitWallet.Tests;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.SegWitWallet
{
    public class SegWitWalletManager
    {
        public static readonly SemaphoreSlim WalletSemaphore = new SemaphoreSlim(1, 1);
        public const string WalletFileExtension = ".keybag.json";
        const string DownloadChainLoop = "SegWitWalletManager.DownloadChain";

        readonly Network network;
        readonly IScriptAddressReader scriptAddressReader;
        readonly ILogger logger;
        readonly IDateTimeProvider dateTimeProvider;
        readonly IBroadcasterManager broadcasterManager;
        readonly ChainIndexer chainIndexer;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;

        public SegWitWalletManager(string walletFilePath, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory, IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider)
        {
            this.CurrentWalletFilePath = walletFilePath;
            this.Wallet = JsonConvert.DeserializeObject<KeyWallet>(File.ReadAllText(walletFilePath));
            if (Path.GetFileName(walletFilePath.Replace(WalletFileExtension, string.Empty)) != this.Wallet.Name)
                throw new InvalidOperationException();

            this.chainIndexer = chainIndexer;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(typeof(SegWitWalletManager).FullName);
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;
            this.broadcasterManager = broadcasterManager;

            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }
        }

        public string CurrentWalletFilePath { get; }
        public KeyWallet Wallet { get; }

        IAsyncLoop asyncLoop;


        

        static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        static readonly Func<string, byte[], byte[]> KeyEncryption = VCL.EncryptWithPassphrase;

        







        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our Wallet and
        // 2. the list of addresses contained in our Wallet for checking whether a transaction is being paid to the Wallet.
        // 3. a mapping of all inputs with their corresponding transactions, to facilitate rapid lookup
        readonly Dictionary<OutPoint, TransactionData> outpointLookup = new Dictionary<OutPoint, TransactionData>();
        readonly ConcurrentDictionary<Script, KeyAddress> scriptToAddressLookup = new ConcurrentDictionary<Script, KeyAddress>();
        readonly Dictionary<OutPoint, TransactionData> inputLookup = new Dictionary<OutPoint, TransactionData>();


      





        #region IWalletManager 

        public void Start()
        {

            // Find wallets and load them in memory.
            //IEnumerable<KeyWallet> wallets = this.fileStorage.LoadByFileExtension(WalletFileExtension);



            // AddAddressesToMaintainBuffer()

            //if (this.walletSettings.IsDefaultWalletEnabled())
            //{
            //    // Check if it already exists, if not, create one.
            //    if (!wallets.Any(w => w.Name == this.walletSettings.DefaultWalletName))
            //    {
            //        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            //        this.CreateWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, string.Empty, mnemonic);
            //    }

            //    // Make sure both unlock is specified, and that we actually have a default Wallet name specified.
            //    if (this.walletSettings.UnlockDefaultWallet)
            //    {
            //        this.UnlockWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, MaxWalletUnlockDurationInSeconds);
            //    }
            //}

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();
            // Find the last chain block received by the Wallet manager.
           

           
        }

        void LoadKeysLookupLock()
        {
            foreach (KeyAddress address in this.Wallet.Addresses)
            {
                this.scriptToAddressLookup[KeyAddressExtensions.GetPaymentScript(address)] = address;
                //if (address.Pubkey != null)
                //    this.scriptToAddressLookup[address.Pubkey] = address;

                foreach (TransactionData transaction in address.Transactions)
                {
                    // Get the UTXOs that are unspent or spent but not confirmed.
                    // We only exclude from the list the confirmed spent UTXOs.
                    if (transaction.SpendingDetails?.BlockHeight == null)
                    {
                        this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                    }
                }
            }
        }

        void UpdateKeysLookupLocked(IEnumerable<KeyAddress> addresses)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            foreach (KeyAddress address in addresses)
            {
                this.scriptToAddressLookup[KeyAddressExtensions.GetPaymentScript(address)] = address;
                //if (address.Pubkey != null)
                //    this.scriptToAddressLookup[address.Pubkey] = address;
            }
        }



        public IEnumerable<UnspentKeyAddressOutput> GetSpendableTransactionsInAccount(string walletName, int confirmations = 0)
        {
            var res = new List<UnspentKeyAddressOutput>();
            foreach (var adr in Wallet.Addresses)
            {
                UnspentKeyAddressOutput[] unspentKeyAddress = GetSpendableTransactions(adr, confirmations);
                res.AddRange(unspentKeyAddress);
            }
            return res;
        }


        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            throw new NotImplementedException();
        }

        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            throw new NotImplementedException();
        }



        public void UnlockWallet(string password, string name, int timeout)
        {
            throw new NotImplementedException();
        }

        public void LockWallet(string name)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime)
        {
            throw new NotImplementedException();
        }

        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }





        public KeyAddress GetUnusedAddress(string walletName)
        {
            return this.GetUnusedAddresses(walletName, 1, false).SingleOrDefault();
        }

        public KeyAddress GetChangeAddress(string walletName)
        {
            return this.Wallet.Addresses.First(a => a.IsChangeAddress());
        }

        public IEnumerable<KeyAddress> GetUnusedAddresses(string walletName, int count, bool isChange = false)
        {
            return this.Wallet.Addresses.Where(a => a.IsChangeAddress() == isChange && a.Transactions.Count == 0)
                .Take(count);

            #region we do not create new addresses here if there are no unused addresses

            //int diff = unusedAddresses.Count() - count;
            //var newAddresses = new List<KeyAddress>();
            //if (diff < 0)
            //{

            //    newAddresses = CreateNdAddresses(Math.Abs(diff), isChange, Wallet).ToList();
            //    foreach (var adr in newAddresses)
            //        Wallet.Addresses.Add(adr);
            //    this.SaveWallet(Wallet);
            //    this.UpdateKeysLookupLocked(newAddresses);
            //}

            //addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.UniqueIndex).Take(count);


            //return addresses.Select(adr => adr.ToFakeHdAddress()).ToList();

            #endregion
        }

        KeyAddress[] CreateNdAddresses(int amountToCreate, bool isChange, KeyWallet wallet)
        {
            throw new NotImplementedException("Creating new addresses will require the passphrase.");
            var newAdresses = new KeyAddress[amountToCreate];
            var oldMaxIndex = wallet.Addresses.Where(a => a.IsChangeAddress() == isChange).Max(a => a.UniqueIndex);
            var newIndex = oldMaxIndex;
            for (var i = 0; i < amountToCreate; i++)
            {
                var privateKey = new byte[32];
                rng.GetBytes(privateKey);
                newIndex += 2;
                // newAdresses[i] = KeyAddress.CreateWithPrivateKey(privateKey, this.network.Consensus.CoinType, newIndex);
            }

            return newAdresses;
        }



        public FlatHistory[] GetHistory()
        {
            // Get transactions contained in the account.
            return this.Wallet.Addresses
                .Where(a => a.Transactions.Any())
                .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s.ToFakeHdAddress(), Transaction = t })).ToArray();
        }

        public IEnumerable<KeyAddressBalance> GetBalances(string walletName, string accountName = null)
        {
            var balances = new List<KeyAddressBalance>();



            foreach (KeyAddress address in this.Wallet.Addresses)
            {
                // Calculates the amount of spendable coins.
                UnspentKeyAddressOutput[] spendableBalance = GetSpendableTransactions(address);
                Money spendableAmount = Money.Zero;
                foreach (UnspentKeyAddressOutput bal in spendableBalance)
                {
                    spendableAmount += bal.Transaction.Amount;
                }



                // Get the total balances.
                (Money amountConfirmed, Money amountUnconfirmed) result = KeyAddressExtensions.GetBalances(address);




                balances.Add(new KeyAddressBalance
                {
                    AmountConfirmed = result.amountConfirmed,
                    AmountUnconfirmed = result.amountUnconfirmed,
                    SpendableAmount = spendableAmount,
                    KeyAddress = address
                });
            }

            return balances;
        }

        UnspentKeyAddressOutput[] GetSpendableTransactions(KeyAddress address, int confirmations = 0)
        {

            var height = this.chainIndexer.Tip.Height;
            var coinbaseMaturity = this.network.Consensus.CoinbaseMaturity;
            var unspendOutputReferences = new List<UnspentKeyAddressOutput>();

            // A block that is at the tip has 1 confirmation.
            // When calculating the confirmations the tip must be advanced by one.
            int countFrom = height + 1;
            var unspentTransActions = address.GetUnspentTransactions();

            foreach (TransactionData transactionData in unspentTransActions)
            {
                int? confirmationCount = 0;
                if (transactionData.BlockHeight != null)
                    confirmationCount = countFrom >= transactionData.BlockHeight ? countFrom - transactionData.BlockHeight : 0;

                if (confirmationCount < confirmations)
                    continue;

                bool isCoinBase = transactionData.IsCoinBase ?? false;
                bool isCoinStake = transactionData.IsCoinStake ?? false;

                // This output can unconditionally be included in the results.
                // Or this output is a CoinBase or CoinStake and has reached maturity.
                if ((!isCoinBase && !isCoinStake) || (confirmationCount > coinbaseMaturity))
                {
                    unspendOutputReferences.Add(new UnspentKeyAddressOutput
                    {
                        Address = address,
                        Transaction = transactionData,
                        Confirmations = confirmationCount.Value
                    });
                }
            }

            return unspendOutputReferences.ToArray();
        }



        public AddressBalance GetAddressBalance(string address)
        {
            throw new NotImplementedException();
        }

        

       



        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));

            var allAddresses = this.scriptToAddressLookup.Values;
            foreach (var address in allAddresses)
            {
                // Remove all the UTXO that have been reorged.
                IEnumerable<TransactionData> makeUnspendable = address.Transactions.Where(w => w.BlockHeight > fork.Height).ToList();
                foreach (TransactionData transactionData in makeUnspendable)
                    address.Transactions.Remove(transactionData);

                // Bring back all the UTXO that are now spendable after the reorg.
                IEnumerable<TransactionData> makeSpendable = address.Transactions.Where(w => (w.SpendingDetails != null) && (w.SpendingDetails.BlockHeight > fork.Height));
                foreach (TransactionData transactionData in makeSpendable)
                    transactionData.SpendingDetails = null;
            }

            this.UpdateLastBlockSyncedHeight(fork);

            // Reload the lookup dictionaries.
            this.RefreshInputKeysLookupLock();
        }

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            var tipHash = this.Wallet.LastBlockSyncedHash;
            var tipHeight = this.Wallet.LastBlockSyncedHeight;
            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != tipHash)
            {
                this.logger.LogTrace("New block's previous hash '{0}' does not match current Wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, tipHash);

                // The block coming in to the Wallet should never be ahead of the Wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > tipHeight)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the Wallet");
                }
            }
            bool trxFoundInBlock = false;
            foreach (Transaction transaction in block.Transactions)
            {
                bool trxFound = this.ProcessTransaction(transaction, chainedHeader.Height, block, true);
                if (trxFound)
                {
                    trxFoundInBlock = true;
                }
            }

            // Update the wallets with the last processed block height.
            // It's important that updating the height happens after the block processing is complete,
            // as if the node is stopped, on re-opening it will start updating from the previous height.
            this.UpdateLastBlockSyncedHeight(chainedHeader);

            if (trxFoundInBlock)
            {
                this.logger.LogDebug("Block {0} contains at least one transaction affecting the user's Wallet(s).", chainedHeader);
            }

        }

        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            if (block != null)
            {
                // Do a pre-scan of the incoming transaction's inputs to see if they're used in other Wallet transactions already.
                foreach (TxIn input in transaction.Inputs)
                {
                    // See if this input is being used by another Wallet transaction present in the index.
                    // The inputs themselves may not belong to the Wallet, but the transaction data in the index has to be for a Wallet transaction.
                    if (this.inputLookup.TryGetValue(input.PrevOut, out TransactionData indexData))
                    {
                        // It's the same transaction, which can occur if the transaction had been added to the Wallet previously. Ignore.
                        if (indexData.Id == hash)
                            continue;

                        if (indexData.BlockHash != null)
                        {
                            // This should not happen as pre checks are done in mempool and consensus.
                            throw new WalletException("The same inputs were found in two different confirmed transactions");
                        }

                        // This is a double spend we remove the unconfirmed trx
                        this.RemoveTransactionsByIds(new[] { indexData.Id });

                        this.inputLookup.Remove(input.PrevOut);
                    }
                }
            }

            // Check the outputs, ignoring the ones with a 0 amount.
            foreach (TxOut utxo in transaction.Outputs.Where(o => o.Value != Money.Zero))
            {
                // Check if the outputs contain one of our addresses.
                if (this.scriptToAddressLookup.TryGetValue(utxo.ScriptPubKey, out KeyAddress _))
                {
                    AddTransactionToWallet(transaction, utxo, blockHeight, block, isPropagated);
                    foundReceivingTrx = true;
                    this.logger.LogDebug("Transaction '{0}' contained funds received by the user's Wallet(s).", hash);
                }
            }

            // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
            foreach (TxIn input in transaction.Inputs)
            {
                if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                {
                    continue;
                }

                // Get the details of the outputs paid out.
                IEnumerable<TxOut> paidOutTo = transaction.Outputs.Where(o =>
                {
                    // If script is empty ignore it.
                    if (o.IsEmpty)
                        return false;

                    // Check if the destination script is one of the Wallet's.
                    bool found = this.scriptToAddressLookup.TryGetValue(o.ScriptPubKey, out KeyAddress addr);

                    // Include the keys not included in our wallets (external payees).
                    if (!found)
                        return true;

                    // Include the keys that are in the Wallet but that are for receiving
                    // addresses (which would mean the user paid itself).
                    // We also exclude the keys involved in a staking transaction.
                    return !addr.IsChangeAddress() && !transaction.IsCoinStake;
                });

                this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                foundSendingTrx = true;
                this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's Wallet(s).", hash);
            }

            return foundSendingTrx || foundReceivingTrx;
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the Wallet for history (and reorg).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="paidToOutputs">A list of payments made out</param>
        /// <param name="spendingTransactionId">The id of the transaction containing the output being spent, if this is a spending transaction.</param>
        /// <param name="spendingTransactionIndex">The index of the output in the transaction being referenced, if this is a spending transaction.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
            uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

            uint256 transactionHash = transaction.GetHash();

            // Get the transaction being spent.
            TransactionData spentTransaction = this.scriptToAddressLookup.Values.Distinct().SelectMany(v => v.Transactions)
                .SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));
            if (spentTransaction == null)
            {
                // Strange, why would it be null?
                this.logger.LogTrace("(-)[TX_NULL]");
                return;
            }

            // If the details of this spending transaction are seen for the first time.
            if (spentTransaction.SpendingDetails == null)
            {
                this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

                var payments = new List<PaymentDetails>();
                foreach (TxOut paidToOutput in paidToOutputs)
                {
                    // Figure out how to retrieve the destination address.
                    string destinationAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, paidToOutput.ScriptPubKey);
                    if (string.IsNullOrEmpty(destinationAddress))
                        if (this.scriptToAddressLookup.TryGetValue(paidToOutput.ScriptPubKey, out KeyAddress destination))
                            destinationAddress = destination.Bech32;

                    payments.Add(new PaymentDetails
                    {
                        DestinationScriptPubKey = paidToOutput.ScriptPubKey,
                        DestinationAddress = destinationAddress,
                        Amount = paidToOutput.Value,
                        OutputIndex = transaction.Outputs.IndexOf(paidToOutput)
                    });
                }

                var spendingDetails = new SpendingDetails
                {
                    TransactionId = transactionHash,
                    Payments = payments,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    BlockHeight = blockHeight,
                    BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
                    Hex = transaction.ToHex(),
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true
                };

                spentTransaction.SpendingDetails = spendingDetails;
                spentTransaction.MerkleProof = null;
            }
            else // If this spending transaction is being confirmed in a block.
            {
                this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

                // Update the block height.
                if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
                {
                    spentTransaction.SpendingDetails.BlockHeight = blockHeight;
                }

                // Update the block time to be that of the block in which the transaction is confirmed.
                if (block != null)
                {
                    spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                    spentTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
                }
            }

            // If the transaction is spent and confirmed, we remove the UTXO from the lookup dictionary.
            if (spentTransaction.SpendingDetails.BlockHeight != null)
            {
                this.outpointLookup.Remove(new OutPoint(spentTransaction.Id, spentTransaction.Index));
            }
        }

       



       



       

       

        



       

       

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(IEnumerable<uint256> transactionsIds)
        {
            List<uint256> idsToRemove = transactionsIds.ToList();
            var result = new HashSet<(uint256, DateTimeOffset)>();

            foreach (KeyAddress adr in this.Wallet.Addresses)
            {
                for (int i = 0; i < adr.Transactions.Count; i++)
                {
                    TransactionData transaction = adr.Transactions.ElementAt(i);

                    // Remove the transaction from the list of transactions affecting an address.
                    // Only transactions that haven't been confirmed in a block can be removed.
                    if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
                    {
                        result.Add((transaction.Id, transaction.CreationTime));
                        adr.Transactions = adr.Transactions.Except(new[] { transaction }).ToList();
                        i--;
                    }

                    // Remove the spending transaction object containing this transaction id.
                    if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
                    {
                        result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
                        adr.Transactions.ElementAt(i).SpendingDetails = null;
                    }
                }
            }


            if (result.Any())
            {
                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();

                this.SaveWallet();
            }

            return result;
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            throw new NotImplementedException();
        }

        #endregion

      

        

        public Network GetNetwork()
        {
            return this.network;
        }

        void SaveWallet()
        {
            var serializedWallet = JsonConvert.SerializeObject(this.Wallet, Formatting.Indented);
            File.WriteAllText(this.CurrentWalletFilePath, serializedWallet);
        }


       

        /// <summary>
        /// Updates details of the last block synced in a Wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the Wallet.</param>
        void UpdateWhenChainDownloaded(DateTime date)
        {
            if (this.asyncProvider.IsAsyncLoopRunning(DownloadChainLoop))
            {
                return;
            }

            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoopUntil(DownloadChainLoop, this.nodeLifetime.ApplicationStopping,
                () => this.chainIndexer.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chainIndexer.GetHeightAtTime(date);

                   
                        this.logger.LogTrace("The chain of headers has finished downloading, updating Wallet '{0}' with height {1}", this.Wallet.Name, heightAtDate);
                        this.UpdateLastBlockSyncedHeight(this.chainIndexer.GetHeader(heightAtDate));
                        this.SaveWallet();
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

                        this.UpdateLastBlockSyncedHeight(this.chainIndexer.Tip);
                },
                TimeSpans.FiveSeconds);
        }




      
        public void Stop()
        {
            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.SaveWallet();
        }

        private void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
            }
            else
            {
                this.logger.LogTrace("Exception occurred: {0}", transactionEntry.ErrorMessage);
                this.logger.LogTrace("(-)[EXCEPTION]");
            }
        }


       
        void AddSegWitAddressesToLookup()
        {

                foreach (KeyAddress adr in Wallet.Addresses)
                {
                    var script = KeyAddressExtensions.GetPaymentScript(adr);
                    //this.scriptToAddressLookup[script] = new HdAddress{ Address = adr.Bech32, Pubkey = script, ScriptPubKey = script};
                }
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate>();
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            throw new NotImplementedException();
        }

        
        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {

            // The block locator will help when the Wallet
            // needs to rewind this will be used to find the fork.
            this.Wallet.BlockLocator = chainedHeader.GetLocator().Blocks;
            this.Wallet.LastBlockSyncedHeight = chainedHeader.Height;
            this.Wallet.LastBlockSyncedHash = chainedHeader.HashBlock;
            SaveWallet();
        }

       

        /// <summary>
        /// Reloads the UTXOs we're tracking in memory for faster lookups.
        /// </summary>
        void RefreshInputKeysLookupLock()
        {
            this.outpointLookup.Clear();

                foreach (KeyAddress address in Wallet.Addresses)
                {
                    // Get the UTXOs that are unspent or spent but not confirmed.
                    // We only exclude from the list the confirmed spent UTXOs.
                    foreach (TransactionData transaction in address.Transactions.Where(t => t.SpendingDetails?.BlockHeight == null))
                    {
                        this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                    }
                }
        }

        /// <summary>
        /// Adds a transaction that credits the Wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the Wallet.</param>
        /// <param name="blockHeight">Height of the block.</param>
        /// <param name="block">The block containing the transaction to add.</param>
        /// <param name="isPropagated">Propagation state of the transaction.</param>
        void AddTransactionToWallet(Transaction transaction, TxOut utxo, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            this.scriptToAddressLookup.TryGetValue(script, out KeyAddress address);
            ICollection<TransactionData> addressTransactions = address.Transactions;

            // Check if a similar UTXO exists or not (same transaction ID and same index).
            // New UTXOs are added, existing ones are updated.
            int index = transaction.Outputs.IndexOf(utxo);
            Money amount = utxo.Value;
            TransactionData foundTransaction = addressTransactions.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
            if (foundTransaction == null)
            {
                this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
                var newTransaction = new TransactionData
                {
                    Amount = amount,
                    IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
                    IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true,
                    BlockHeight = blockHeight,
                    BlockHash = block?.GetHash(),
                    BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
                    Id = transactionHash,
                    CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
                    Index = index,
                    ScriptPubKey = script,
                    Hex = transaction.ToHex(),
                    IsPropagated = isPropagated,
                };

                // Add the Merkle proof to the (non-spending) transaction.
                if (block != null)
                {
                    newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                addressTransactions.Add(newTransaction);
                this.outpointLookup[new OutPoint(newTransaction.Id, newTransaction.Index)] = newTransaction;

                if (block == null)
                {
                    // Unconfirmed inputs track for double spends.
                    foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                    {
                        this.inputLookup[input] = newTransaction;
                    }
                }
            }
            else
            {
                this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

                // Update the block height and block hash.
                if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
                {
                    foundTransaction.BlockHeight = blockHeight;
                    foundTransaction.BlockHash = block?.GetHash();
                    foundTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
                }

                // Update the block time.
                if (block != null)
                {
                    foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
                }

                // Add the Merkle proof now that the transaction is confirmed in a block.
                if ((block != null) && (foundTransaction.MerkleProof == null))
                {
                    foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
                }

                if (isPropagated)
                    foundTransaction.IsPropagated = true;

                if (block != null)
                {
                    // Inputs are in a block no need to track them anymore.
                    foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                    {
                        this.inputLookup.Remove(input);
                    }
                }
            }


            this.TransactionFoundInternal(script);
        }

        void TransactionFoundInternal(Script script, Func<HdAccount, bool> accountFilter = null)
        {
            return;  // this method ensures there are enough unused addresses in the Wallet. not sure if that will be supported here.

            
                bool isChange;
                if (Wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == false))
                {
                    isChange = false;
                }
                else if (Wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == true))
                {
                    isChange = true;
                }
              

                // IEnumerable<NDAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

                // this.UpdateKeysLookupLocked(newAddresses);


        }

        private IEnumerable<KeyAddress> AddAddressesToMaintainBuffer(object account, bool isChange)
        {
            throw new NotImplementedException();
        }


    }
}
