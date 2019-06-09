using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Obsidian.Features.SegWitWallet.Tests;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.SegWitWallet
{
    public class SegWitWalletManager : IWalletManager
    {
        const string DownloadChainLoop = "SegWitWalletManager.DownloadChain";
        const string WalletFileExtension = "keybag.json"; // do not use 'wallet' in this string
        const int WalletSaveIntervalMinutes = 5;

        static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        readonly Network network;
        readonly IScriptAddressReader scriptAddressReader;
        readonly ILogger logger;
        readonly IDateTimeProvider dateTimeProvider;
        readonly IAsyncProvider asyncProvider;
        readonly INodeLifetime nodeLifeTime;
        readonly IBroadcasterManager broadcasterManager;
        readonly FileStorage<KeyWallet> fileStorage;
        readonly ChainIndexer chainIndexer;

        readonly ConcurrentDictionary<string, KeyWallet> wallets = new ConcurrentDictionary<string, KeyWallet>();
        readonly object lockObject = new object();




        IAsyncLoop asyncLoop;


        // In order to allow faster look-ups of transactions affecting the wallets' addresses,
        // we keep a couple of objects in memory:
        // 1. the list of unspent outputs for checking whether inputs from a transaction are being spent by our wallet and
        // 2. the list of addresses contained in our wallet for checking whether a transaction is being paid to the wallet.
        // 3. a mapping of all inputs with their corresponding transactions, to facilitate rapid lookup
        readonly Dictionary<OutPoint, TransactionData> outpointLookup = new Dictionary<OutPoint, TransactionData>();
        readonly ConcurrentDictionary<Script, KeyAddress> scriptToAddressLookup = new ConcurrentDictionary<Script, KeyAddress>();
        readonly Dictionary<OutPoint, TransactionData> inputLookup = new Dictionary<OutPoint, TransactionData>();


        /// <summary>
        /// Constructs the cold staking manager which is used by the cold staking controller.
        /// </summary>
        /// <param name="network">The network that the manager is running on.</param>
        /// <param name="chainIndexer">Thread safe class representing a chain of headers from genesis.</param>
        /// <param name="dataFolder">Contains path locations to folders and files on disk.</param>
        /// <param name="walletFeePolicy">The wallet fee policy.</param>
        /// <param name="asyncProvider">Factory for creating and also possibly starting application defined tasks inside async loop.</param>
        /// <param name="nodeLifeTime">Allows consumers to perform cleanup during a graceful shutdown.</param>
        /// <param name="scriptAddressReader">A reader for extracting an address from a <see cref="Script"/>.</param>
        /// <param name="loggerFactory">The logger factory to use to create the custom logger.</param>
        /// <param name="dateTimeProvider">Provider of time functions.</param>
        /// <param name="broadcasterManager">The broadcaster manager.</param>
        public SegWitWalletManager(
            Network network,
            ChainIndexer chainIndexer,
            DataFolder dataFolder,
            IWalletFeePolicy walletFeePolicy,
            IAsyncProvider asyncProvider,
            INodeLifetime nodeLifeTime,
            IScriptAddressReader scriptAddressReader,
            ILoggerFactory loggerFactory,
            IDateTimeProvider dateTimeProvider,
            IBroadcasterManager broadcasterManager = null)
        {
            Guard.NotNull(loggerFactory, nameof(loggerFactory));
            Guard.NotNull(dateTimeProvider, nameof(dateTimeProvider));

            this.network = network;
            this.scriptAddressReader = scriptAddressReader;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.dateTimeProvider = dateTimeProvider;
            this.asyncProvider = asyncProvider;
            this.nodeLifeTime = nodeLifeTime;
            this.fileStorage = new FileStorage<KeyWallet>(dataFolder.WalletPath);
            this.chainIndexer = chainIndexer;
            this.broadcasterManager = broadcasterManager;
            if (this.broadcasterManager != null)
            {
                this.broadcasterManager.TransactionStateChanged += this.BroadcasterManager_TransactionStateChanged;
            }
        }


        /// <summary>
        /// Loads the wallet to be used by the manager if a wallet with this name has not already been loaded.
        /// </summary>
        /// <param name="wallet">The wallet to load.</param>
        void Load(KeyWallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            if (this.wallets.TryGetValue(wallet.Name, out _))
            {
                this.logger.LogTrace("(-)[NOT_FOUND]");  // should be FOUND
                return;
            }

            this.wallets[wallet.Name] = wallet;
        }

        #region IWalletManager 

        public void Start()
        {

            // Find wallets and load them in memory.
            IEnumerable<KeyWallet> wallets = this.fileStorage.LoadByFileExtension(WalletFileExtension);

            foreach (KeyWallet wallet in wallets)
            {
                Load(wallet);
                //AddAddressesToMaintainBuffer()
            }

            //if (this.walletSettings.IsDefaultWalletEnabled())
            //{
            //    // Check if it already exists, if not, create one.
            //    if (!wallets.Any(w => w.Name == this.walletSettings.DefaultWalletName))
            //    {
            //        var mnemonic = new Mnemonic(Wordlist.English, WordCount.Twelve);
            //        this.CreateWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, string.Empty, mnemonic);
            //    }

            //    // Make sure both unlock is specified, and that we actually have a default wallet name specified.
            //    if (this.walletSettings.UnlockDefaultWallet)
            //    {
            //        this.UnlockWallet(this.walletSettings.DefaultWalletPassword, this.walletSettings.DefaultWalletName, MaxWalletUnlockDurationInSeconds);
            //    }
            //}

            // Load data in memory for faster lookups.
            this.LoadKeysLookupLock();
            // Find the last chain block received by the wallet manager.
            HashHeightPair hashHeightPair = this.LastReceivedBlockInfo();
            this.WalletTipHash = hashHeightPair.Hash;
            this.WalletTipHeight = hashHeightPair.Height;

            // Save the wallets file every 5 minutes to help against crashes.
            this.asyncLoop = this.asyncProvider.CreateAndRunAsyncLoop("Wallet persist job", token =>
            {
                this.SaveWallets();
                this.logger.LogInformation("Wallets saved to file at {0}.", this.dateTimeProvider.GetUtcNow());

                this.logger.LogTrace("(-)[IN_ASYNC_LOOP]");
                return Task.CompletedTask;
            },
            this.nodeLifeTime.ApplicationStopping,
            repeatEvery: TimeSpan.FromMinutes(WalletSaveIntervalMinutes),
            startAfter: TimeSpan.FromMinutes(WalletSaveIntervalMinutes));
        }

        void LoadKeysLookupLock()
        {
            lock (this.lockObject)
            {
                foreach (KeyWallet wallet in this.wallets.Values)
                {

                    foreach (KeyAddress address in wallet.Addresses)
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
            }
        }

        void UpdateKeysLookupLocked(IEnumerable<KeyAddress> addresses)
        {
            if (addresses == null || !addresses.Any())
            {
                return;
            }

            lock (this.lockObject)
            {
                foreach (KeyAddress address in addresses)
                {
                    this.scriptToAddressLookup[KeyAddressExtensions.GetPaymentScript(address)] = address;
                    //if (address.Pubkey != null)
                    //    this.scriptToAddressLookup[address.Pubkey] = address;
                }
            }
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            return GetSpendableTransactionsInAccount(
                new WalletAccountReference { WalletName = walletName, AccountName = "account 0" }, confirmations);
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            Guard.NotNull(walletAccountReference, nameof(walletAccountReference));

            KeyWallet wallet = this.wallets[walletAccountReference.WalletName];
            var res = new List<UnspentOutputReference>();
            lock (this.lockObject)
            {
                foreach (var adr in wallet.Addresses)
                {
                    UnspentKeyAddressOutput[] unspentKeyAddress = GetSpendableTransactions(this.chainIndexer.Tip.Height, this.network.Consensus.CoinbaseMaturity, adr, confirmations);
                    foreach (var u in unspentKeyAddress)
                    {
                        var r = new UnspentOutputReference
                        {
                            Account = wallet.Addresses.ToFakeHdAccount(wallet),
                            Address = u.Address.ToFakeHdAddress(),
                            Confirmations = u.Confirmations,
                            Transaction = u.Transaction
                        };
                        res.Add(r);
                    }
                }
            }
            return res;
        }

        public Mnemonic CreateWallet(string password, string name, string passphrase, Mnemonic mnemonic = null)
        {
            throw new NotSupportedException($"Creating a HD wallet is not supported. To create a nondeterministic wallet that contains just a bunch of keys with {nameof(SegWitWalletManager)}, please cast IWalletManager to {nameof(SegWitWalletManager)} and call {nameof(CreateNondeterministicWallet)}.");

        }

        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            throw new NotImplementedException();
        }

        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            throw new NotImplementedException();
        }

        public Wallet LoadWallet(string password, string name)
        {
            Guard.NotEmpty(password, nameof(password));
            Guard.NotEmpty(name, nameof(name));

            // Load the file from the local system.
            KeyWallet wallet = this.fileStorage.LoadByFileName($"{name}.{WalletFileExtension}");

            // Check the password.
            try
            {
                if (password != "q")
                    throw new Exception("Password must be 'q'");
            }
            catch (Exception ex)
            {
                this.logger.LogTrace("Exception occurred: {0}", ex.ToString());
                this.logger.LogTrace("(-)[EXCEPTION]");
                throw new SecurityException(ex.Message);
            }

            this.Load(wallet);

            return null;
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

        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            throw new NotImplementedException();
        }

        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
            throw new NotImplementedException();
        }

        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1, false).Single();
            return res;

        }

        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            HdAddress res = this.GetUnusedAddresses(accountReference, 1, true).Single();
            return res;
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.Assert(count > 0);

            KeyWallet wallet = this.wallets[accountReference.WalletName];

            IEnumerable<KeyAddress> addresses;

            lock (this.lockObject)
            {
                IEnumerable<KeyAddress> unusedAddresses = wallet.Addresses
                                                        .Where(a => a.IsChangeAddress() == isChange && a.Transactions.Count == 0)
                                                        .Take(count).ToList();


                int diff = unusedAddresses.Count() - count;
                var newAddresses = new List<KeyAddress>();
                if (diff < 0)
                {

                    newAddresses = CreateNdAddresses(Math.Abs(diff), isChange, wallet).ToList();
                    foreach (var adr in newAddresses)
                        wallet.Addresses.Add(adr);
                    this.SaveWallet(wallet);
                    this.UpdateKeysLookupLocked(newAddresses);
                }

                addresses = unusedAddresses.Concat(newAddresses).OrderBy(x => x.UniqueIndex).Take(count);
            }



            List<HdAddress> viewModel = new List<HdAddress>();
            foreach (var adr in addresses)
                viewModel.Add(adr.ToFakeHdAddress());
            return viewModel;
        }

        KeyAddress[] CreateNdAddresses(int amountToCreate, bool isChange, KeyWallet wallet)
        {
            var newAdresses = new KeyAddress[amountToCreate];
            var oldMaxIndex = wallet.Addresses.Where(a => a.IsChangeAddress() == isChange).Max(a => a.UniqueIndex);
            var newIndex = oldMaxIndex;
            for (var i = 0; i < amountToCreate; i++)
            {
                var privateKey = new byte[32];
                rng.GetBytes(privateKey);
                newIndex += 2;
                newAdresses[i] = KeyAddress.CreateWithPrivateKey(privateKey, this.network.Consensus.CoinType, newIndex);
            }

            return newAdresses;
        }

        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            Guard.NotEmpty(walletName, nameof(walletName));

            var fakeHd = new HdAccount() { Name = walletName };
            // In order to calculate the fee properly we need to retrieve all the transactions with spending details.
            var accountsHistory = new List<AccountHistory>();
            accountsHistory.Add(GetHistory(fakeHd));


            return accountsHistory;
        }

        public AccountHistory GetHistory(HdAccount account)
        {
            var walletName = account.Name; // hack
            KeyWallet wallet = this.wallets[walletName];

            Guard.NotNull(account, nameof(account));

            FlatHistory[] items;
            lock (this.lockObject)
            {
                var combinedAddresses = wallet.Addresses;
                // Get transactions contained in the account.
                items = combinedAddresses
                    .Where(a => a.Transactions.Any())
                    .SelectMany(s => s.Transactions.Select(t => new FlatHistory { Address = s.ToFakeHdAddress(), Transaction = t })).ToArray();
            }

            return new AccountHistory { Account = account, History = items };
        }

        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null)
        {
            var balances = new List<AccountBalance>();

            lock (this.lockObject)
            {
                KeyWallet wallet = this.wallets[walletName];

                var fakeAccount = wallet.Addresses.ToFakeHdAccount(wallet);

                foreach (KeyAddress address in wallet.Addresses)
                {
                    // Calculates the amount of spendable coins.
                    UnspentKeyAddressOutput[] spendableBalance = GetSpendableTransactions(this.chainIndexer.Tip.Height, this.network.Consensus.CoinbaseMaturity, address);
                    Money spendableAmount = Money.Zero;
                    foreach (UnspentKeyAddressOutput bal in spendableBalance)
                    {
                        spendableAmount += bal.Transaction.Amount;
                    }



                    // Get the total balances.
                    (Money amountConfirmed, Money amountUnconfirmed) result = KeyAddressExtensions.GetBalances(address);




                    balances.Add(new AccountBalance
                    {
                        AmountConfirmed = result.amountConfirmed,
                        AmountUnconfirmed = result.amountUnconfirmed,
                        SpendableAmount = spendableAmount,
                        Account = fakeAccount
                    });
                }
            }

            return balances;
        }

        UnspentKeyAddressOutput[] GetSpendableTransactions(int currentChainHeight, long coinbaseMaturity, KeyAddress address, int confirmations = 0)
        {
            var unspendOutputReferences = new List<UnspentKeyAddressOutput>();

            // A block that is at the tip has 1 confirmation.
            // When calculating the confirmations the tip must be advanced by one.

            int countFrom = currentChainHeight + 1;
            var unspentTransActions = KeyAddressExtensions.GetUnspentTransactions(address);
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

        public Wallet GetWallet(string walletName)
        {
            return null;
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            throw new NotImplementedException();
        }

        public int LastBlockHeight()
        {
            lock (this.lockObject)
            {
                foreach (var w in this.wallets.Values)
                {
                    if (w is KeyWallet wal)
                    {
                        return wal.LastBlockSyncedHeight ?? 0;
                    }

                    // return ((Wallet)w).AccountsRoot.Single().LastBlockSyncedHeight ?? 0;
                }
            }
            return this.chainIndexer.Tip.Height;
        }

        public void RemoveBlocks(ChainedHeader fork)
        {
            Guard.NotNull(fork, nameof(fork));

            lock (this.lockObject)
            {
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
        }

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            Guard.NotNull(block, nameof(block));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // If there is no wallet yet, update the wallet tip hash and do nothing else.
            if (this.wallets.Count == 0)
            {
                this.WalletTipHash = chainedHeader.HashBlock;
                this.WalletTipHeight = chainedHeader.Height;
                this.logger.LogTrace("(-)[NO_WALLET]");
                return;
            }
            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock != this.WalletTipHash)
            {
                this.logger.LogTrace("New block's previous hash '{0}' does not match current wallet's tip hash '{1}'.", chainedHeader.Header.HashPrevBlock, this.WalletTipHash);

                // The block coming in to the wallet should never be ahead of the wallet.
                // If the block is behind, let it pass.
                if (chainedHeader.Height > this.WalletTipHeight)
                {
                    this.logger.LogTrace("(-)[BLOCK_TOO_FAR]");
                    throw new WalletException("block too far in the future has arrived to the wallet");
                }
            }
            lock (this.lockObject)
            {
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
                    this.logger.LogDebug("Block {0} contains at least one transaction affecting the user's wallet(s).", chainedHeader);
                }
            }

        }

        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            Guard.NotNull(transaction, nameof(transaction));
            uint256 hash = transaction.GetHash();

            bool foundReceivingTrx = false, foundSendingTrx = false;

            lock (this.lockObject)
            {
                if (block != null)
                {
                    // Do a pre-scan of the incoming transaction's inputs to see if they're used in other wallet transactions already.
                    foreach (TxIn input in transaction.Inputs)
                    {
                        // See if this input is being used by another wallet transaction present in the index.
                        // The inputs themselves may not belong to the wallet, but the transaction data in the index has to be for a wallet transaction.
                        if (this.inputLookup.TryGetValue(input.PrevOut, out TransactionData indexData))
                        {
                            // It's the same transaction, which can occur if the transaction had been added to the wallet previously. Ignore.
                            if (indexData.Id == hash)
                                continue;

                            if (indexData.BlockHash != null)
                            {
                                // This should not happen as pre checks are done in mempool and consensus.
                                throw new WalletException("The same inputs were found in two different confirmed transactions");
                            }

                            // This is a double spend we remove the unconfirmed trx
                            foreach (var wallet in this.wallets.Values)
                            {
                                this.RemoveTransactionsByIds(wallet.Name, new[] { indexData.Id });
                            }

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
                        this.logger.LogDebug("Transaction '{0}' contained funds received by the user's wallet(s).", hash);
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

                        // Check if the destination script is one of the wallet's.
                        bool found = this.scriptToAddressLookup.TryGetValue(o.ScriptPubKey, out KeyAddress addr);

                        // Include the keys not included in our wallets (external payees).
                        if (!found)
                            return true;

                        // Include the keys that are in the wallet but that are for receiving
                        // addresses (which would mean the user paid itself).
                        // We also exclude the keys involved in a staking transaction.
                        return !addr.IsChangeAddress() && !transaction.IsCoinStake;
                    });

                    this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                    foundSendingTrx = true;
                    this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's wallet(s).", hash);
                }
            }

            return foundSendingTrx || foundReceivingTrx;
        }

        /// <summary>
        /// Mark an output as spent, the credit of the output will not be used to calculate the balance.
        /// The output will remain in the wallet for history (and reorg).
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
                lock (this.lockObject)
                {
                    this.outpointLookup.Remove(new OutPoint(spentTransaction.Id, spentTransaction.Index));
                }

            }
        }

        public void SaveWallet(Wallet wallet)
        {
            throw new NotImplementedException();
        }

        public void SaveWallets()
        {
            foreach (var wallet in this.wallets.Values)
                SaveWallet(wallet);
        }

        public string GetWalletFileExtension()
        {
            return WalletFileExtension;
        }

        public IEnumerable<string> GetWalletsNames()
        {
            return this.wallets.Keys;
        }



        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            // Update the wallets with the last processed block height.
            foreach (KeyWallet wallet in this.wallets.Values)
            {
                this.UpdateLastBlockSyncedHeight(wallet, chainedHeader);
            }

            this.WalletTipHash = chainedHeader.HashBlock;
            this.WalletTipHeight = chainedHeader.Height;
        }

        public Wallet GetWalletByName(string walletName)
        {
            return null;
        }

        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            return this.wallets.Values.First().BlockLocator;
        }

        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            return (this.fileStorage.FolderPath, this.fileStorage.GetFilesNames(this.GetWalletFileExtension()));
        }

        public bool ContainsWallets => this.wallets.Count > 0;

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false)
        {
            throw new NotImplementedException();
        }

        public int? GetEarliestWalletHeight()
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset GetOldestWalletCreationTime()
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            Guard.NotNull(transactionsIds, nameof(transactionsIds));
            Guard.NotEmpty(walletName, nameof(walletName));

            List<uint256> idsToRemove = transactionsIds.ToList();
            KeyWallet wallet = this.wallets[walletName];

            var result = new HashSet<(uint256, DateTimeOffset)>();

            lock (this.lockObject)
            {

                foreach (KeyAddress adr in wallet.Addresses)
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

            }

            if (result.Any())
            {
                // Reload the lookup dictionaries.
                this.RefreshInputKeysLookupLock();

                this.SaveWallet(wallet);
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

        public void CreateNondeterministicWallet(string name, string password)
        {
            try
            {
                if (this.wallets.ContainsKey(name))
                    throw new InvalidOperationException($"A wallet with name {name} is already loaded.");

                if (this.fileStorage.Exists($"{name}.{WalletFileExtension}"))
                    throw new InvalidOperationException(
                        $"A wallet with name {name} is already present in the data folder!");

                var wal = new KeyWallet
                {
                    Name = name,
                    CreationTime = DateTime.UtcNow,
                    WalletType = nameof(KeyWallet),
                    WalletTypeVersion = 1,
                    Addresses = new List<KeyAddress>()
                };
                var uniqueIndex = 0;
                var adr = KeyAddress.CreateWithPrivateKey(StaticWallet.Key1Bytes, this.network.Consensus.CoinType, uniqueIndex++, 0, "odx");
                wal.Addresses.Add(adr);
                var adr2 = KeyAddress.CreateWithPrivateKey(StaticWallet.Key2Bytes, this.network.Consensus.CoinType, uniqueIndex++, 0, "odx");
                wal.Addresses.Add(adr2);



                UpdateKeysLookupLocked(wal.Addresses);
                // If the chain is downloaded, we set the height of the newly created wallet to it.
                // However, if the chain is still downloading when the user creates a wallet,
                // we wait until it is downloaded in order to set it. Otherwise, the height of the wallet will be the height of the chain at that moment.
                if (this.chainIndexer.IsDownloaded())
                {
                    this.UpdateLastBlockSyncedHeight(wal, this.chainIndexer.Tip);
                }
                else
                {
                    this.UpdateWhenChainDownloaded(new[] { wal }, this.dateTimeProvider.GetUtcNow());
                }

                // Save the changes to the file and add addresses to be tracked.
                this.SaveWallet(wal);
                if (!this.wallets.TryAdd(wal.Name, wal))
                    throw new InvalidOperationException($"A wallet with name {name} is already loaded.");
            }
            catch (Exception e)
            {
                this.logger.LogError($"Could not create wallet: {e.Message}");
            }

        }

        public KeyWallet GetSegWitWallet(string name)
        {
            return this.wallets[name];
        }

        public Network GetNetwork()
        {
            return this.network;
        }

        void SaveWallet(KeyWallet wallet)
        {
            Guard.NotNull(wallet, nameof(wallet));

            lock (this.lockObject)
            {
                this.fileStorage.SaveToFile(wallet, $"{wallet.Name}.{WalletFileExtension}", new FileStorageOption { SerializeNullValues = false });
            }
        }


        void UpdateLastBlockSyncedHeight(KeyWallet wallet, ChainedHeader chainedHeader)
        {
            Guard.NotNull(wallet, nameof(wallet));
            Guard.NotNull(chainedHeader, nameof(chainedHeader));

            lock (this.lockObject)
            {
                // The block locator will help when the wallet
                // needs to rewind this will be used to find the fork.
                wallet.BlockLocator = chainedHeader.GetLocator().Blocks;
                wallet.LastBlockSyncedHeight = chainedHeader.Height;
                wallet.LastBlockSyncedHash = chainedHeader.HashBlock;
            }
        }

        /// <summary>
        /// Updates details of the last block synced in a wallet when the chain of headers finishes downloading.
        /// </summary>
        /// <param name="wallets">The wallets to update when the chain has downloaded.</param>
        /// <param name="date">The creation date of the block with which to update the wallet.</param>
        void UpdateWhenChainDownloaded(IEnumerable<KeyWallet> wallets, DateTime date)
        {
            if (this.asyncProvider.IsAsyncLoopRunning(DownloadChainLoop))
            {
                return;
            }

            this.asyncProvider.CreateAndRunAsyncLoopUntil(DownloadChainLoop, this.nodeLifeTime.ApplicationStopping,
                () => this.chainIndexer.IsDownloaded(),
                () =>
                {
                    int heightAtDate = this.chainIndexer.GetHeightAtTime(date);

                    foreach (KeyWallet wallet in wallets)
                    {
                        this.logger.LogTrace("The chain of headers has finished downloading, updating wallet '{0}' with height {1}", wallet.Name, heightAtDate);
                        this.UpdateLastBlockSyncedHeight(wallet, this.chainIndexer.GetHeader(heightAtDate));
                        this.SaveWallet(wallet);
                    }
                },
                (ex) =>
                {
                    // In case of an exception while waiting for the chain to be at a certain height, we just cut our losses and
                    // sync from the current height.
                    this.logger.LogError($"Exception occurred while waiting for chain to download: {ex.Message}");

                    foreach (KeyWallet wallet in wallets)
                    {
                        this.UpdateLastBlockSyncedHeight(wallet, this.chainIndexer.Tip);
                    }
                },
                TimeSpans.FiveSeconds);
        }




        /// <summary>
        /// Gets the hash of the last block received by the wallets.
        /// </summary>
        /// <returns>Hash of the last block received by the wallets.</returns>
        public HashHeightPair LastReceivedBlockInfo()
        {
            if (!this.wallets.Any())
            {
                ChainedHeader chainedHeader = this.chainIndexer.Tip;
                this.logger.LogTrace("(-)[NO_WALLET]:'{0}'", chainedHeader);
                return new HashHeightPair(chainedHeader);
            }

            uint256 lastBlockSyncedHash = null;
            int lastSyncedBlockHeight = 0;
            lock (this.lockObject)
            {

                foreach (var w in this.wallets.Values)
                {
                    if (w.LastBlockSyncedHeight.HasValue && w.LastBlockSyncedHeight.Value > lastSyncedBlockHeight)
                    {
                        lastSyncedBlockHeight = w.LastBlockSyncedHeight.Value;
                        lastBlockSyncedHash = w.LastBlockSyncedHash;
                    }

                }


                // If details about the last block synced are not present in the wallet,
                // find out which is the oldest wallet and set the last block synced to be the one at this date.
                if (lastBlockSyncedHash == null)
                {
                    this.logger.LogWarning("There were no details about the last block synced in the wallets.");
                    DateTimeOffset earliestWalletDate = this.wallets.Values.Min(c => c.CreationTime);
                    this.UpdateWhenChainDownloaded(this.wallets.Values, earliestWalletDate.DateTime);
                    return new HashHeightPair(this.chainIndexer.Tip);
                }
            }

            return new HashHeightPair(lastBlockSyncedHash, lastSyncedBlockHeight);
        }
        public void Stop()
        {
            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= this.BroadcasterManager_TransactionStateChanged;

            this.asyncLoop?.Dispose();
            this.SaveWallets();
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


        public uint256 WalletTipHash { get; set; }
        public int WalletTipHeight { get; set; }

        void AddSegWitAddressesToLookup()
        {

            lock (this.lockObject)
            {
                foreach (KeyWallet wallet in this.wallets.Values)
                {
                    foreach (KeyAddress adr in wallet.Addresses)
                    {
                        var script = KeyAddressExtensions.GetPaymentScript(adr);
                        //this.scriptToAddressLookup[script] = new HdAddress{ Address = adr.Bech32, Pubkey = script, ScriptPubKey = script};
                    }
                }
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

        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Reloads the UTXOs we're tracking in memory for faster lookups.
        /// </summary>
        void RefreshInputKeysLookupLock()
        {
            lock (this.lockObject)
            {
                this.outpointLookup.Clear();

                foreach (KeyWallet wallet in this.wallets.Values)
                {
                    foreach (KeyAddress address in wallet.Addresses)
                    {
                        // Get the UTXOs that are unspent or spent but not confirmed.
                        // We only exclude from the list the confirmed spent UTXOs.
                        foreach (TransactionData transaction in address.Transactions.Where(t => t.SpendingDetails?.BlockHeight == null))
                        {
                            this.outpointLookup[new OutPoint(transaction.Id, transaction.Index)] = transaction;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a transaction that credits the wallet with new coins.
        /// This method is can be called many times for the same transaction (idempotent).
        /// </summary>
        /// <param name="transaction">The transaction from which details are added.</param>
        /// <param name="utxo">The unspent output to add to the wallet.</param>
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
                lock (this.lockObject)
                {
                    this.outpointLookup[new OutPoint(newTransaction.Id, newTransaction.Index)] = newTransaction;
                }

                if (block == null)
                {
                    // Unconfirmed inputs track for double spends.
                    lock (this.lockObject)
                    {
                        foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                        {
                            this.inputLookup[input] = newTransaction;
                        }
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
                    lock (this.lockObject)
                    {
                        foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
                        {
                            this.inputLookup.Remove(input);
                        }
                    }
                }
            }


            this.TransactionFoundInternal(script);
        }

        void TransactionFoundInternal(Script script, Func<HdAccount, bool> accountFilter = null)
        {
            return;  // this method ensures there are enough unused addresses in the wallet. not sure if that will be supported here.

            foreach (KeyWallet wallet in this.wallets.Values)
            {
                bool isChange;
                if (wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == false))
                {
                    isChange = false;
                }
                else if (wallet.Addresses.Any(address => KeyAddressExtensions.GetPaymentScript(address) == script && address.IsChangeAddress() == true))
                {
                    isChange = true;
                }
                else
                {
                    continue;
                }

                // IEnumerable<NDAddress> newAddresses = this.AddAddressesToMaintainBuffer(account, isChange);

                // this.UpdateKeysLookupLocked(newAddresses);


            }
        }

        private IEnumerable<KeyAddress> AddAddressesToMaintainBuffer(object account, bool isChange)
        {
            throw new NotImplementedException();
        }


    }
}
