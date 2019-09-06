using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Temp;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    public class WalletManager : IDisposable
    {
        public const string WalletFileExtension = ".keybag.json";
        const string DownloadChainLoop = nameof(DownloadChainLoop);
        static TimeSpan AutoSaveInterval = new TimeSpan(0, 1, 0);
        static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
        static readonly Func<string, byte[], byte[]> KeyEncryption = VCL.EncryptWithPassphrase;

        public readonly SemaphoreSlim WalletSemaphore = new SemaphoreSlim(1, 1);
        readonly Dictionary<OutPoint, TransactionData> outpointLookup = new Dictionary<OutPoint, TransactionData>();
        readonly Dictionary<OutPoint, TransactionData> inputLookup = new Dictionary<OutPoint, TransactionData>();


        readonly Network network;
        readonly IScriptAddressReader scriptAddressReader;
        readonly ILogger logger;
        readonly IDateTimeProvider dateTimeProvider;
        readonly IBroadcasterManager broadcasterManager;
        readonly ChainIndexer chainIndexer;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;

        // for staking
        readonly IPosMinting posMinting;
        readonly ITimeSyncBehaviorState timeSyncBehaviorState;

        KeyWallet Wallet { get; }

        IAsyncLoop asyncLoop;
        readonly IAsyncLoop autoSaveWallet;

        #region c'tor

        public WalletManager(string walletFilePath, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory, IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider, IPosMinting posMinting, ITimeSyncBehaviorState timeSyncBehaviorState)
        {
            this.CurrentWalletFilePath = walletFilePath;
            this.Wallet = JsonConvert.DeserializeObject<KeyWallet>(File.ReadAllText(walletFilePath));
            if (Path.GetFileName(walletFilePath.Replace(WalletFileExtension, string.Empty)) != this.Wallet.Name)
                throw new InvalidOperationException();

            this.chainIndexer = chainIndexer;
            this.network = network;
            this.logger = loggerFactory.CreateLogger(typeof(WalletManager).FullName);
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            this.posMinting = posMinting;
            this.timeSyncBehaviorState = timeSyncBehaviorState;

            this.broadcasterManager = broadcasterManager;

            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }

            this.autoSaveWallet = this.asyncProvider.CreateAndRunAsyncLoop(nameof(AutoSaveWalletAsync), AutoSaveWalletAsync, this.nodeLifetime.ApplicationStopping, AutoSaveInterval, AutoSaveInterval);

            RefreshDictionary_OutpointTransactionData();
        }

        #endregion


        #region public get-only properties

        public string CurrentWalletFilePath { get; }

        public string WalletName
        {
            get
            {
                Guard.NotNull(this.Wallet, nameof(this.Wallet));
                return this.Wallet?.Name;
            }
        }

        internal LoadWalletResponse LoadWallet()
        {
            return new LoadWalletResponse { PassphraseChallenge = this.Wallet.PassphraseChallenge.ToHexString() };
        }

        public int WalletLastBlockSyncedHeight
        {
            get
            {
                Guard.NotNull(this.Wallet, nameof(this.Wallet));
                return this.Wallet.LastBlockSyncedHeight;
            }
        }

        public uint256 WalletLastBlockSyncedHash
        {
            get
            {
                Guard.NotNull(this.Wallet, nameof(this.Wallet));
                return this.Wallet.LastBlockSyncedHash;
            }
        }

        public ICollection<uint256> WalletBlockLocator
        {
            get
            {
                Guard.NotNull(this.Wallet, nameof(this.Wallet));
                return this.Wallet.BlockLocator;
            }
        }

        public DateTimeOffset WalletCreationTime
        {
            get
            {
                Guard.NotNull(this.Wallet, nameof(this.Wallet));
                return this.Wallet.CreationTime;
            }
        }

        internal async Task<ExportKeysResponse> ExportKeysAsync(ExportKeysRequest exportKeysRequest)
        {
            var header = new StringBuilder();
            header.AppendLine($"Starting export from wallet {this.Wallet.Name}, network {this.network.Name} on {DateTime.UtcNow} UTC.");
            var errors = new StringBuilder();
            errors.AppendLine("Errors");
            var success = new StringBuilder();
            success.AppendLine("Exported Private Key (Hex); Unix Time UTC; IsChange; Address; Label:");
            int errorCount = 0;
            int successCount = 0;
            try
            {
                var addresses = this.Wallet.Addresses.Values.ToArray();
                header.AppendLine($"{addresses.Length} found in wallet.");

                var enc = new Bech32Encoder($"{this.network.CoinTicker.ToLowerInvariant()}key");

                foreach (var a in addresses)
                {
                    try
                    {
                        var decryptedKey = VCL.DecryptWithPassphrase(exportKeysRequest.WalletPassphrase, a.EncryptedPrivateKey);
                        if (decryptedKey == null)
                        {
                            errorCount++;
                            header.AppendLine(
                                $"Address '{a.Bech32}' with label '{a.Label}' could not be decrpted with this passphrase.");
                        }
                        else
                        {
                            var privateKey = enc.Encode(0, decryptedKey);
                            success.AppendLine($"{privateKey}; {((DateTimeOffset)a.CreatedDateUtc).ToUnixTimeSeconds()}; {a.IsChange}; {a.Bech32}; {a.Label}");
                            successCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        header.AppendLine($"Exception processing Address '{a.Bech32}' with label '{a.Label}':{e.Message}");
                    }
                }

                header.AppendLine($"{errorCount} errors occured.");
                header.AppendLine($"{successCount} addresses with private keys successfully exported.");
            }
            catch (Exception e)
            {
                errors.AppendLine(e.Message);
                return new ExportKeysResponse { Message = $"Export failed because an exception occured: {e.Message}" };
            }

            return new ExportKeysResponse
            { Message = $"{header}{Environment.NewLine}{success}{Environment.NewLine}{errors}{Environment.NewLine}" };
        }

        #endregion

        #region public methods

        public async Task<ImportKeysResponse> ImportKeysAsync(ImportKeysRequest importKeysRequest)
        {
            if (importKeysRequest == null)
                throw new ArgumentNullException(nameof(importKeysRequest));
            if (importKeysRequest.WalletPassphrase == null)
                throw new ArgumentNullException(nameof(importKeysRequest.WalletPassphrase));
            if (importKeysRequest.Keys == null)
                throw new ArgumentNullException(nameof(importKeysRequest.Keys));

            var delimiters = new HashSet<char>();
            foreach (var c in importKeysRequest.Keys.Trim().ToCharArray())
            {
                if (char.IsWhiteSpace(c))
                    delimiters.Add(c);
            }

            var items = importKeysRequest.Keys.Split(delimiters.ToArray());
            var possibleKeys = items.Where(i => i.Length == 52).Distinct().ToList();
            if (possibleKeys.Count == 0)
                throw new X1WalletException(HttpStatusCode.BadRequest,"Input material cointained no keys.",null);

            var firstExistingEncryptedKey = Wallet.Addresses.Values.First().EncryptedPrivateKey;
            var test = VCL.DecryptWithPassphrase(importKeysRequest.WalletPassphrase, firstExistingEncryptedKey);
            if (test == null)
                throw new X1WalletException(System.Net.HttpStatusCode.Unauthorized,
                    "Your passphrase is incorrect.", null);
            var importedAddresses = new List<string>();

            if (true)
            {
                int start = 23;
                int end = 43;
                for (var i = start; i <= end; i++)
                {
                    var bytes = new byte[32];
                    StaticWallet.Fill((byte)i, bytes);
                    var isChange = i % 2 == 0;
                    var address = KeyAddress.CreateWithPrivateKey(bytes, importKeysRequest.WalletPassphrase, VCL.EncryptWithPassphrase, this.network.Consensus.CoinType, 0, this.network.CoinTicker.ToLowerInvariant());
                    address.IsChange = isChange;
                    address.Label = i.ToString();
                    if (!this.Wallet.Addresses.ContainsKey(address.Hash160Hex))
                        this.Wallet.Addresses.Add(address.Hash160Hex, address);
                }
            }




            foreach (var candidate in possibleKeys)
            {
                try
                {
                    var secret = new BitcoinSecret(candidate, this.network);
                    var privateKey = secret.PrivateKey.ToBytes();
                    var address = KeyAddress.CreateWithPrivateKey(privateKey, importKeysRequest.WalletPassphrase,
                        VCL.EncryptWithPassphrase, this.network.Consensus.CoinType, 0,
                        this.network.CoinTicker.ToLowerInvariant());
                    address.IsChange = false;
                    address.Label = secret.GetAddress().ToString();
                    if (!importKeysRequest.Keys.Contains(address.Label))
                        throw new InvalidOperationException();
                    this.Wallet.Addresses.Add(address.Hash160Hex, address);
                    importedAddresses.Add(address.Label);
                }
                catch (Exception e)
                {

                }

            }

            SaveWallet();
            var response = new ImportKeysResponse
            { ImportedAddresses = possibleKeys, Message = $"Imported {importedAddresses.Count} addresses." };
            return response;
        }



        public List<UnspentKeyAddressOutput> GetAllSpendableTransactions(int confirmations = 0)
        {
            var res = new List<UnspentKeyAddressOutput>();
            foreach (var adr in this.Wallet.Addresses.Values)
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

        public KeyAddress GetUnusedAddress()
        {
            return this.GetUnusedAddresses(1, false).SingleOrDefault();
        }

        public KeyAddress GetChangeAddress()
        {
            return this.Wallet.Addresses.Values.First(a => a.IsChange);
        }

        public IEnumerable<KeyAddress> GetUnusedAddresses(int count, bool isChange = false)
        {
            return this.Wallet.Addresses.Values.Where(a => a.IsChange == isChange && a.Transactions.Count == 0)
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


        public List<FlatAddressHistory> GetHistory()// TODO: make sure resyncing also locks the wallet via semaphore, otherwise calls such as getHistory will throw collection modified
        {
            var histories = new List<FlatAddressHistory>();

            foreach (var a in this.Wallet.Addresses.Values)
            {
                foreach (var t in a.Transactions)
                {
                    var h = new FlatAddressHistory { Address = a, Transaction = t };
                    histories.Add(h);
                }
            }

            return histories;
        }

        public Script GetUnusedChangeAddress()
        {
            var unusedChangeAddress = this.Wallet.Addresses.Values.First(a => a.IsChange && a.Transactions.Count == 0);
            return unusedChangeAddress.ScriptPubKey;
        }

        public IEnumerable<KeyAddressBalance> GetBalances()
        {
            var balances = new List<KeyAddressBalance>();



            foreach (KeyAddress address in this.Wallet.Addresses.Values)
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

        public void RemoveBlocks(ChainedHeader chainedHeader)
        {

            foreach (var address in this.Wallet.Addresses.Values)
            {

                for (var j = 0; j < address.Transactions.Count; j++)
                {
                    var tx = address.Transactions[j];

                    //  Remove tx later than height                 
                    if (tx.BlockHeight > chainedHeader.Height)
                        address.Transactions.Remove(tx);
                }

                for (var j = 0; j < address.Transactions.Count; j++)
                {
                    var tx = address.Transactions[j];

                    //  Bring back all the UTXO that are now spendable again after the rewind          
                    if ((tx.SpendingDetails != null) && (tx.SpendingDetails.BlockHeight > chainedHeader.Height))
                        address.Transactions[j].SpendingDetails = null;
                }

                // Update outpointLookup
                this.outpointLookup.Clear();
                for (var j = 0; j < address.Transactions.Count; j++)
                {
                    // Get the UTXOs that are unspent or spent but not confirmed.
                    // We only exclude from the list the confirmed spent UTXOs.
                    var tx = address.Transactions[j];
                    if (tx.SpendingDetails?.BlockHeight == null)
                    {
                        this.outpointLookup[new OutPoint(tx.Id, tx.Index)] = tx;
                    }

                }
            }

            // Update last block synced height
            this.Wallet.BlockLocator = chainedHeader.GetLocator().Blocks;
            this.Wallet.LastBlockSyncedHeight = chainedHeader.Height;
            this.Wallet.LastBlockSyncedHash = chainedHeader.HashBlock;

            SaveWallet();
        }

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            var tipHash = this.Wallet.LastBlockSyncedHash;
            var tipHeight = this.Wallet.LastBlockSyncedHeight;

            if (chainedHeader.Height == 890022)
                ; // I staked!

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
                var address = FindAddressByScriptPubKey(utxo.ScriptPubKey);
                if (address != null)
                {
                    AddTransactionToWallet(transaction, utxo, address, blockHeight, block, isPropagated);
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
                    {
                        return false;
                    }


                    // Check if the destination script is one of the Wallet's.
                    //bool found = this.scriptToAddressLookup.TryGetValue(o.ScriptPubKey, out KeyAddress addr);
                    bool found = false;
                    KeyAddress address = FindAddressByScriptPubKey(o.ScriptPubKey);
                    if (address != null)
                    {
                        AddTransactionToWallet(transaction, o, address, blockHeight, block, isPropagated);
                        foundReceivingTrx = true;
                        this.logger.LogDebug("Transaction '{0}' contained funds received by the user's Wallet(s).", hash);
                        found = true;
                    }
                    else
                    {
                        // Include the keys not included in our wallets (external payees).
                        //if (!found)
                        //    return true;
                        return true;
                    }

                    // Include the keys that are in the Wallet but that are for receiving
                    // addresses (which would mean the user paid itself).
                    // We also exclude the keys involved in a staking transaction.
                    return !address.IsChange && !transaction.IsCoinStake;
                });

                this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                foundSendingTrx = true;
                this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's Wallet(s).", hash);
            }
            return foundSendingTrx || foundReceivingTrx;
        }

        public KeyAddressesModel GetAllAddresses()
        {
            return new KeyAddressesModel
            {
                Addresses = this.Wallet.Addresses.Values.Select(address => new KeyAddressModel
                {
                    Address = address.Bech32,
                    IsUsed = address.Transactions.Any(),
                    IsChange = address.IsChange,
                    EncryptedPrivateKey = address.EncryptedPrivateKey
                }).ToArray()
            };
        }

        public void StartStaking(string passphrase)
        {
            Guard.NotNull(passphrase, nameof(passphrase));

            if (VCL.DecryptWithPassphrase(passphrase, Wallet.Addresses.Values.First().EncryptedPrivateKey) == null)
                throw new X1WalletException(HttpStatusCode.Unauthorized, "The passphrase is not correct.", null);

            if (!this.network.Consensus.IsProofOfStake)
                throw new X1WalletException(HttpStatusCode.BadRequest, "Staking requires a Proof-of-Stake consensus.", null);

            if (this.timeSyncBehaviorState.IsSystemTimeOutOfSync)
            {
                string errorMessage = "Staking cannot start, your system time does not match that of other nodes on the network." + Environment.NewLine
                                                                                                                                  + "Please adjust your system time and restart the node.";
                this.logger.LogError(errorMessage);
                throw new X1WalletException(HttpStatusCode.InternalServerError, errorMessage, null);
            }

            this.logger.LogInformation("Enabling staking on wallet '{0}'.", this.WalletName);

            this.posMinting.Stake(new WalletSecret
            {
                WalletPassword = passphrase,
                WalletName = this.WalletName
            });
        }

        internal void StopStaking()
        {
            this.posMinting.StopStake();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(IEnumerable<uint256> transactionsIds)
        {
            List<uint256> idsToRemove = transactionsIds.ToList();
            var result = new HashSet<(uint256, DateTimeOffset)>();

            foreach (KeyAddress adr in this.Wallet.Addresses.Values)
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
                this.RefreshDictionary_OutpointTransactionData();

                this.SaveWallet();
            }

            return result;
        }

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate>();
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            throw new NotImplementedException();
        }
        public void Dispose()
        {
            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.autoSaveWallet?.Dispose();
        }

        #endregion

        #region private Methods

        KeyAddress FindAddressByScriptPubKey(Script scriptPubKey)
        {
            byte[] raw = scriptPubKey.ToBytes();
            byte[] hash160 = null;
            var pubKey = scriptPubKey.GetDestinationPublicKeys(this.network).SingleOrDefault();
            if (pubKey != null)
            {
                hash160 = pubKey.Hash.ToBytes();
            }
            else if(raw.Length == 22 && raw[0] == 0 && raw[1] == 20)  // push 20 (x14) bytes
            {
                hash160 = raw.Skip(2).Take(20).ToArray();
            }
            else
            {
                hash160 = scriptPubKey.Hash.ToBytes();
            }

            Debug.Assert(hash160.Length == 20);
            var key = hash160.ToHexString();
            this.Wallet.Addresses.TryGetValue(key, out KeyAddress keyAddress);
            return keyAddress;
        }

        void RefreshDictionary_OutpointTransactionData()
        {
            this.outpointLookup.Clear();

            foreach (KeyAddress address in this.Wallet.Addresses.Values)
            {
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

        internal HashSet<(uint256 transactionId, DateTimeOffset creationTime)> RemoveTransactionsFromDate(string walletName, DateTime fromDate)
        {
            throw new NotImplementedException();
        }

        internal HashSet<(uint256 transactionId, DateTimeOffset creationTime)> RemoveAllTransactions(string walletName)
        {
            throw new NotImplementedException();
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

            IEnumerable<TransactionData> allTransactionData = this.Wallet.Addresses.Values.SelectMany(v => v.Transactions);
            var spentTransaction = allTransactionData.SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));

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
                    {
                        var destination = FindAddressByScriptPubKey(paidToOutput.ScriptPubKey);
                        if (destination != null)
                        {
                            destinationAddress = destination.Bech32;
                        }
                    }


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

        void SaveWallet(bool isAutoSave = false)
        {
            var serializedWallet = JsonConvert.SerializeObject(this.Wallet, Formatting.Indented);
            File.WriteAllText(this.CurrentWalletFilePath, serializedWallet);
            if (!isAutoSave)
                this.logger.LogInformation("Saved on explicit request.");
        }

        async Task AutoSaveWalletAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;
            try
            {
                await this.WalletSemaphore.WaitAsync(cancellationToken);
                SaveWallet(true);
            }
            finally
            {
                this.WalletSemaphore.Release();
            }

            this.logger.LogInformation("Auto-saved wallet at {0}.", this.dateTimeProvider.GetUtcNow());
            this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");
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

        void BroadcasterManager_TransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
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

        /// <summary>
        /// Saves the tip and BlockLocator from chainedHeader to the wallet file.
        /// </summary>
        void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {

            // The block locator will help when the Wallet
            // needs to rewind this will be used to find the fork.
            this.Wallet.BlockLocator = chainedHeader.GetLocator().Blocks;
            this.Wallet.LastBlockSyncedHeight = chainedHeader.Height;
            this.Wallet.LastBlockSyncedHash = chainedHeader.HashBlock;
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
        void AddTransactionToWallet(Transaction transaction, TxOut utxo, KeyAddress address, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            Guard.NotNull(utxo, nameof(utxo));

            uint256 transactionHash = transaction.GetHash();

            // Get the collection of transactions to add to.
            Script script = utxo.ScriptPubKey;
            //this.scriptToAddressLookup.TryGetValue(script, out KeyAddress address);
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


            //bool isChange;
            //if (this.Wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == false))
            //{
            //    isChange = false;
            //}
            //else if (this.Wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == true))
            //{
            //    isChange = true;
            //}


            // IEnumerable<NDAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

            // this.UpdateKeysLookupLocked(newAddresses);


        }

        #endregion


    }
}
