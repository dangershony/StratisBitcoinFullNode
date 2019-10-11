using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using NBitcoin.DataEncoders;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    public class WalletManager : IDisposable
    {
        public const int ExpectedMetadataVersion = 1;
        public readonly SemaphoreSlim WalletSemaphore = new SemaphoreSlim(1, 1);

        static readonly TimeSpan AutoSaveInterval = new TimeSpan(0, 1, 0);

        readonly Network network;
        readonly DataFolder dataFolder;
        readonly IScriptAddressReader scriptAddressReader;
        readonly ILogger logger;
        readonly IDateTimeProvider dateTimeProvider;
        readonly IBroadcasterManager broadcasterManager;
        readonly ChainIndexer chainIndexer;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;
        readonly ISignals signals;
        readonly IInitialBlockDownloadState initialBlockDownloadState;
        readonly IBlockStore blockStore;

        // for staking
        readonly IPosMinting posMinting;
        readonly ITimeSyncBehaviorState timeSyncBehaviorState;

        X1WalletFile X1WalletFile { get; }
        X1WalletMetadataFile Metadata { get; }

        SubscriptionToken blockConnectedSubscription;
        SubscriptionToken transactionReceivedSubscription;

        bool isStartingUp;
        Stopwatch startupStopwatch;
        long startupDuration;
        Timer startupTimer;

        #region c'tor and initialisation

        public WalletManager(string x1WalletFilePath, ChainIndexer chainIndexer, Network network, DataFolder dataFolder,
            IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory,
            IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime,
            IAsyncProvider asyncProvider, IPosMinting posMinting, ITimeSyncBehaviorState timeSyncBehaviorState,
            ISignals signals, IInitialBlockDownloadState initialBlockDownloadState, IBlockStore blockStore)
        {
            this.CurrentX1WalletFilePath = x1WalletFilePath;

            this.X1WalletFile = WalletHelper.LoadX1WalletFile(x1WalletFilePath);
            this.CurrentX1WalletMetadataFilePath =
                this.X1WalletFile.WalletName.GetX1WalletMetaDataFilepath(network, dataFolder);
            this.Metadata = WalletHelper.LoadOrCreateX1WalletMetadataFile(this.CurrentX1WalletMetadataFilePath,
                this.X1WalletFile, ExpectedMetadataVersion, network.GenesisHash.ToString());
            P2WPKHAddressExtensions.Metadata = this.Metadata;

            this.chainIndexer = chainIndexer;
            this.network = network;
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(typeof(WalletManager).FullName);
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            this.posMinting = posMinting;
            this.timeSyncBehaviorState = timeSyncBehaviorState;

            this.broadcasterManager = broadcasterManager;
            this.signals = signals;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.blockStore = blockStore;

            ScheduleSyncing();
        }

        void ScheduleSyncing()
        {
            this.isStartingUp = true;

            if (this.startupStopwatch == null)
                this.startupStopwatch = new Stopwatch();

            this.startupTimer = new Timer(_ =>
            {
                this.startupTimer.Dispose();
                SyncWallet();

            }, null, 500, Timeout.Infinite);
        }

        void CompleteStart()
        {
            this.isStartingUp = false;
            this.startupStopwatch.Stop();
            this.startupStopwatch = null;
            this.startupTimer = null;
            this.SaveMetadata();

            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged += OnTransactionStateChanged;
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(OnBlockConnected);
            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(OnTransactionReceived);

        }

        public void Dispose()
        {
            if (this.broadcasterManager != null)
                this.broadcasterManager.TransactionStateChanged -= OnTransactionStateChanged;

            if (this.transactionReceivedSubscription != null)
                this.signals.Unsubscribe(this.transactionReceivedSubscription);

            if (this.blockConnectedSubscription != null)
                this.signals.Unsubscribe(this.blockConnectedSubscription);
        }

        internal LoadWalletResponse LoadWallet()
        {
            return new LoadWalletResponse { PassphraseChallenge = this.X1WalletFile.PassphraseChallenge.ToHexString() };
        }

        #endregion


        #region public get-only properties

        public string CurrentX1WalletFilePath { get; }
        public string CurrentX1WalletMetadataFilePath { get; }

        public string WalletName => this.X1WalletFile.WalletName;

        public int WalletLastBlockSyncedHeight => this.Metadata.SyncedHeight;

        public string WalletLastBlockSyncedHash => this.Metadata.SyncedHash;

        #endregion

        #region syncing

        void SyncWallet() // semaphore?
        {
            if (this.initialBlockDownloadState.IsInitialBlockDownload())
            {
                this.logger.LogInformation("Wallet is waiting for IBD to complete.");
                return;
            }

            this.startupStopwatch?.Restart();

            try
            {
                this.WalletSemaphore.Wait();

                // a) check if the wallet is on the right chain
                if (!IsOnBestChain())
                {
                    MoveToBestChain();
                }

                // if we are here, we are on the best chain and the information about the tip in the Metadata file is correct.

                // b) now let the wallet catch up


                this.logger.LogInformation(
                    $"Wallet {this.WalletName} is at block {this.Metadata.SyncedHeight}, catching up, {TimeSpan.FromMilliseconds(this.startupDuration).Duration()} elapsed.");

                while (this.chainIndexer.Tip.Height > this.Metadata.SyncedHeight)
                {
                    // this can take a long time, so watch for cancellation
                    if (this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        SaveMetadata();
                        return;
                    }

                    if (this.isStartingUp && this.startupStopwatch.ElapsedMilliseconds >= 5000)
                    {
                        SaveMetadata();
                        this.startupDuration += this.startupStopwatch.ElapsedMilliseconds;
                        ScheduleSyncing();
                        return;
                    }

                    var nextBlockForWalletHeight = this.Metadata.SyncedHeight + 1;
                    ChainedHeader nextBlockForWalletHeader = this.chainIndexer.GetHeader(nextBlockForWalletHeight);
                    Block nextBlockForWallet = this.blockStore.GetBlock(nextBlockForWalletHeader.HashBlock);
                    ProcessBlock(nextBlockForWallet, nextBlockForWalletHeader);
                }
            }
            catch (Exception e)
            {
                this.logger.LogError($"{nameof(SyncWallet)}: {e.Message}");
            }
            finally
            {
                this.WalletSemaphore.Release();
            }

            if (this.isStartingUp)
                CompleteStart();
        }

        /// <summary>
        /// Checks if the wallet is on the right chain.
        /// </summary>
        /// <returns>true, if on the right chain.</returns>
        bool IsOnBestChain()
        {
            bool isOnBestChain;
            if (this.Metadata.SyncedHeight == 0 || this.Metadata.SyncedHash.IsDefault())
            {
                // if the height is 0, we cannot be on the wrong chain
                ResetMetadata();
                isOnBestChain = true;

            }
            else
            {
                // check if the wallet tip hash is in the current consensus chain
                isOnBestChain = this.chainIndexer.GetHeader(this.Metadata.SyncedHash.ToUInt256()) != null;
            }

            return isOnBestChain;
        }

        /// <summary>
        /// If IsOnBestChain returns false, we need to fix this by removing the fork blocks from the wallet.
        /// </summary>
        void MoveToBestChain()
        {
            ChainedHeader checkpointHeader = null;
            if (!this.Metadata.CheckpointHash.IsDefault())
            {
                var header = this.chainIndexer.GetHeader(this.Metadata.CheckpointHash.ToUInt256());
                if (header != null && this.Metadata.CheckpointHeight == header.Height)
                    checkpointHeader = header;  // the checkpoint header is in the correct chain and the the checkpoint height in the wallet is consistent
            }
            if (checkpointHeader != null && this.chainIndexer.Tip.Height - checkpointHeader.Height > this.network.Consensus.MaxReorgLength)  // also check the checkpoint is not newer than it should be
            {
                // we have a valid checkpoint, remove all later blocks
                RemoveBlocks(checkpointHeader);
            }
            else
            {
                // we do not have a usable checkpoint, sync from start by resetting everything
                ResetMetadata();
            }
        }

        /// <summary>
        /// It is assumed that the argument contains the header of the highest block (inclusive) where the wallet data is
        /// consistent with the right chain.
        /// This method removes all block and the transactions in them of later blocks.
        /// </summary>
        /// <param name="checkpointHeader">ChainedHeader of the checkpoint</param>
        public void RemoveBlocks(ChainedHeader checkpointHeader)
        {
            var blocksAfterCheckpoint = this.Metadata.Blocks.Keys.Where(x => x > checkpointHeader.Height).ToArray();
            foreach (var height in blocksAfterCheckpoint)
                this.Metadata.Blocks.Remove(height);

            // Update outpointLookup
            //RefreshDictionary_OutpointTransactionData();

            // Update last block synced height
            this.Metadata.SyncedHeight = checkpointHeader.Height;
            this.Metadata.SyncedHash = checkpointHeader.HashBlock.ToString();
            this.Metadata.CheckpointHeight = checkpointHeader.Height;
            this.Metadata.CheckpointHash = checkpointHeader.HashBlock.ToString();
            SaveMetadata();
        }

        #endregion

        void OnTransactionReceived(TransactionReceived transactionReceived)
        {
            // semaphore
            this.logger.LogInformation($"WalletManager.OnTransactionReceived: Transaction {transactionReceived.ReceivedTransaction.GetHash()} received.");
            // ProcessTransaction(transactionReceived.ReceivedTransaction);
        }

        void OnBlockConnected(BlockConnected blockConnected)
        {
            this.logger.LogInformation($"WalletManager.OnBlockConnected: Block {blockConnected.ConnectedBlock.ChainedHeader.Height} connected.");
            SyncWallet();
        }

        #region import/export keys

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
                throw new X1WalletException(HttpStatusCode.BadRequest, "Input material cointained no keys.", null);

            var test = VCL.DecryptWithPassphrase(importKeysRequest.WalletPassphrase, this.X1WalletFile.PassphraseChallenge);
            if (test == null)
                throw new X1WalletException(System.Net.HttpStatusCode.Unauthorized,
                    "Your passphrase is incorrect.", null);
            var importedAddresses = new List<string>();

            var obsidianNetwork = new ObsidianNetwork();

            foreach (var candidate in possibleKeys)
            {
                try
                {
                    var secret = new BitcoinSecret(candidate, obsidianNetwork);
                    var privateKey = secret.PrivateKey.ToBytes();
                    var address = P2WpkhAddress.CreateWithPrivateKey(privateKey, importKeysRequest.WalletPassphrase,
                        VCL.EncryptWithPassphrase, false, this.network.Consensus.CoinType, 0, this.network.CoinTicker.ToLowerInvariant(), this.network.CoinTicker);

                    this.X1WalletFile.P2WPKHAddresses.Add(address.HashHex, address);
                    secret.GetAddress().ToString();
                    importedAddresses.Add($"{secret.GetAddress().ToString()} -> {address.Address}");
                }
                catch (Exception e)
                {

                }

            }

            this.X1WalletFile.SaveX1WalletFile(this.CurrentX1WalletFilePath);

            var response = new ImportKeysResponse
            { ImportedAddresses = importedAddresses, Message = $"Imported {importedAddresses.Count} addresses." };
            return response;
        }

        internal async Task<ExportKeysResponse> ExportKeysAsync(ExportKeysRequest exportKeysRequest)
        {
            var header = new StringBuilder();
            header.AppendLine($"Starting export from wallet {this.X1WalletFile.WalletName}, network {this.network.Name} on {DateTime.UtcNow} UTC.");
            var errors = new StringBuilder();
            errors.AppendLine("Errors");
            var success = new StringBuilder();
            success.AppendLine("Exported Private Key (Hex); Unix Time UTC; IsChange; Address; Label:");
            int errorCount = 0;
            int successCount = 0;
            try
            {
                var addresses = this.X1WalletFile.P2WPKHAddresses.Values;
                header.AppendLine($"{this.X1WalletFile.P2WPKHAddresses.Count} found in wallet.");

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
                                $"Address '{a.Address}'  could not be decrpted with this passphrase.");
                        }
                        else
                        {
                            var privateKey = enc.Encode(0, decryptedKey);
                            success.AppendLine($"{privateKey}; {a.IsChange}; {a.Address}");
                            successCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        header.AppendLine($"Exception processing Address '{a.Address}': {e.Message}");
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



        internal Dictionary<string, P2WpkhAddress> GetAllAddresses()
        {
            return this.X1WalletFile.P2WPKHAddresses;
        }

        /// <summary>
        /// Clears and initializes the wallet Metadata file, and sets heights to 0 and the hashes to null,
        /// and saves the Metadata file, effectively updating it to the latest version.
        /// </summary>
        internal void ResetMetadata()
        {
            this.Metadata.MetadataVersion = ExpectedMetadataVersion;
            this.Metadata.SyncedHash = this.network.GenesisHash.ToString();
            this.Metadata.SyncedHeight = 0;
            this.Metadata.CheckpointHash = this.Metadata.SyncedHash;
            this.Metadata.CheckpointHeight = 0;
            this.Metadata.Blocks = new Dictionary<int, BlockMetadata>();
            this.Metadata.WalletGuid = this.X1WalletFile.WalletGuid;

            SaveMetadata();
        }

        public List<UnspentKeyAddressOutput> GetAllSpendableTransactions(int confirmations = 0)
        {
            var res = new List<UnspentKeyAddressOutput>();
            var coins = new List<Coin>();
            foreach (var block in this.Metadata.Blocks)
            {
                foreach (KeyValuePair<string, TransactionMetadata> tx in block.Value.ConfirmedTransactions)
                {
                    foreach (UtxoMetadata o in tx.Value.ReceivedUtxos)
                    {
                        coins.Add(new Coin(new uint256(tx.Key), (uint)o.Index, Money.Satoshis(o.Satoshis), this.X1WalletFile.P2WPKHAddresses[o.HashHex].GetScriptPubKey()));
                    }

                }
            }
            return res;
        }

        public List<Coin> GetAllSpendableCoins()
        {
            var coins = new List<Coin>();
            foreach (KeyValuePair<int, BlockMetadata> block in this.Metadata.Blocks)
            {
                foreach (KeyValuePair<string, TransactionMetadata> tx in block.Value.ConfirmedTransactions)
                {
                    if (tx.Value.IsCoinbase)
                    {
                        var txAge = this.chainIndexer.Tip.Height - block.Key;

                        if (txAge < this.network.Consensus.CoinbaseMaturity)
                            continue;
                    }

                    foreach (UtxoMetadata o in tx.Value.ReceivedUtxos)
                    {
                        coins.Add(new Coin(new uint256(tx.Key), (uint)o.Index, Money.Satoshis(o.Satoshis), this.X1WalletFile.P2WPKHAddresses[o.HashHex].GetScriptPubKey()));
                    }

                }
            }
            return coins;
        }




        public P2WpkhAddress GetUnusedAddress()
        {
            foreach (P2WpkhAddress address in this.X1WalletFile.P2WPKHAddresses.Values)
            {
                if (IsAddressUsedInConfirmedTransactions(address))
                    continue;
                return address;
            }
            return null;
        }

        bool IsAddressUsedInConfirmedTransactions(P2WpkhAddress address)
        {
            // slow version
            foreach (BlockMetadata block in this.Metadata.Blocks.Values)
            {
                foreach (TransactionMetadata tx in block.ConfirmedTransactions.Values)
                {
                    foreach (var utxo in tx.ReceivedUtxos)
                    {
                        if (utxo.HashHex == address.HashHex)
                            return true;
                    }

                }
            }
            return false;
        }



        public Balance GetConfirmedWalletBalance()
        {
            Dictionary<string, TransactionMetadata> confirmedTransactions = new Dictionary<string, TransactionMetadata>();

            foreach (BlockMetadata block in this.Metadata.Blocks.Values)
            {
                foreach (TransactionMetadata tx in block.ConfirmedTransactions.Values)
                {
                    foreach (UtxoMetadata utxo in tx.ReceivedUtxos)
                    {
                        if (this.X1WalletFile.P2WPKHAddresses.ContainsKey(utxo.HashHex))
                        {
                            confirmedTransactions.Add(tx.HashTx, tx);
                            break;
                        }
                    }

                }
            }

            long totalReceived = 0;

            foreach (var tx in confirmedTransactions.Values)
            {
                foreach (var utxo in tx.ReceivedUtxos)
                {
                    if (this.X1WalletFile.P2WPKHAddresses.ContainsKey(utxo.HashHex))
                    {
                        totalReceived += utxo.Satoshis;
                        break;
                    }
                }

            }

            var balance = new Balance
            {
                AmountConfirmed = Money.Satoshis(totalReceived),
                AmountUnconfirmed = Money.Zero,
                SpendableAmount = totalReceived / 10
            };
            return balance;
        }


      

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            var tipHash = this.Metadata.SyncedHash;
            var tipHeight = this.Metadata.SyncedHeight;


            // Is this the next block.
            if (chainedHeader.Header.HashPrevBlock.ToString() != tipHash)
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
            this.UpdateLastBlockSyncedAndCheckpoint(chainedHeader);

            if (trxFoundInBlock)
            {
                this.logger.LogDebug("Block {0} contains at least one transaction affecting the user's Wallet(s).", chainedHeader);
            }

        }

        TransactionMetadata ExtractWalletTransaction(Transaction transaction)
        {
            List<UtxoMetadata> receivedUtxos = null;
            int index = 0;
            foreach (var output in transaction.Outputs)
            {
                P2WpkhAddress ownAddress = FindAddressByScriptPubKey(output.ScriptPubKey);
                if (ownAddress != null)
                {
                    if (receivedUtxos == null)
                        receivedUtxos = new List<UtxoMetadata>();
                    receivedUtxos.Add(new UtxoMetadata { HashHex = ownAddress.HashHex, HashTx = transaction.GetHash().ToString(), Satoshis = output.Value.Satoshi, Index = index });
                }
                index++;
            }
            if (receivedUtxos != null)
            {
                // so this transaction funds this wallet, we could also save information about the other outputs, so that we can provide a better display, but not now...
                var tx = new TransactionMetadata
                {
                    HashTx = transaction.GetHash().ToString(),
                    IsCoinbase = transaction.IsCoinBase,
                    IsCoinstake = transaction.IsCoinStake,
                    ReceivedUtxos = receivedUtxos
                };
                return tx;
            }
            return null;
        }

        bool AreInputsInWallet(Transaction transaction)
        {
            return false;
        }

        bool AreOutputsInWallet(Transaction transaction)
        {
            return false;
        }

        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            if (blockHeight == null || block == null)
            {
                // TODO: process unconfirmed tx separately, do not put them in the wallet file
                this.logger.LogWarning("X1Wallet.WalletManager: Processing mempool tx is not yet implemented!");
            }
            else
            {
                var tx = ExtractWalletTransaction(transaction);
                if (tx != null)
                {
                    if (!this.Metadata.Blocks.ContainsKey(blockHeight.Value))
                        this.Metadata.Blocks.Add(blockHeight.Value, new BlockMetadata { HashBlock = block.GetHash().ToString(), ConfirmedTransactions = new Dictionary<string, TransactionMetadata>() });

                    this.Metadata.Blocks[blockHeight.Value].ConfirmedTransactions.Add(tx.HashTx, tx);
                    return true;
                }

            }

            return false;


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
                    //if (this.inputLookup.TryGetValue(input.PrevOut, out TransactionData indexData))
                    //{
                    //    // It's the same transaction, which can occur if the transaction had been added to the Wallet previously. Ignore.
                    //    if (indexData.Id == hash)
                    //        continue;

                    //    if (indexData.BlockHash != null)
                    //    {
                    //        // This should not happen as pre checks are done in mempool and consensus.
                    //        throw new WalletException("The same inputs were found in two different confirmed transactions");
                    //    }

                    //    // This is a double spend we remove the unconfirmed trx
                    //    //this.RemoveTransactionsByIds(new[] { indexData.Id });

                    //    this.inputLookup.Remove(input.PrevOut);
                    //}
                }
            }

            // Check the outputs, ignoring the ones with a 0 amount.
            foreach (TxOut utxo in transaction.Outputs.Where(o => o.Value != Money.Zero))
            {
                var address = FindAddressByScriptPubKey(utxo.ScriptPubKey);
                if (address != null)
                {
                    //AddTransactionToWallet(transaction, utxo, address, this.Metadata, blockHeight, block, isPropagated);
                    foundReceivingTrx = true;
                    this.logger.LogDebug("Transaction '{0}' contained funds received by the user's Wallet(s).", hash);
                }
            }

            // Check the inputs - include those that have a reference to a transaction containing one of our scripts and the same index.
            foreach (TxIn input in transaction.Inputs)
            {
                //if (!this.outpointLookup.TryGetValue(input.PrevOut, out TransactionData tTx))
                //{
                //    continue;
                //}

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
                    var address = FindAddressByScriptPubKey(o.ScriptPubKey);
                    if (address != null)
                    {
                        //AddTransactionToWallet(transaction, o, address, this.Metadata, blockHeight, block, isPropagated);
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

                //this.AddSpendingTransactionToWallet(transaction, paidOutTo, tTx.Id, tTx.Index, blockHeight, block);
                foundSendingTrx = true;
                this.logger.LogDebug("Transaction '{0}' contained funds sent by the user's Wallet(s).", hash);
            }
            return foundSendingTrx || foundReceivingTrx;
        }



        public void StartStaking(string passphrase)
        {
            Guard.NotNull(passphrase, nameof(passphrase));

            if (VCL.DecryptWithPassphrase(passphrase, this.X1WalletFile.PassphraseChallenge) == null)
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

        //public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(IEnumerable<uint256> transactionsIds)
        //{
        //    List<uint256> idsToRemove = transactionsIds.ToList();
        //    var result = new HashSet<(uint256, DateTimeOffset)>();

        //    foreach (P2WpkhAddress addr in this.X1WalletFile.P2WPKHAddresses.Values)
        //    {
        //        var txs = addr.GetTransactionsByAddress();

        //        for (int i = 0; i < txs.Count; i++)
        //        {
        //            TransactionData transaction = txs.ElementAt(i);

        //            // Remove the transaction from the list of transactions affecting an address.
        //            // Only transactions that haven't been confirmed in a block can be removed.
        //            if (!transaction.IsConfirmed() && idsToRemove.Contains(transaction.Id))
        //            {
        //                result.Add((transaction.Id, transaction.CreationTime));
        //                txs = txs.Except(new[] { transaction }).ToList();
        //                i--;
        //            }

        //            // Remove the spending transaction object containing this transaction id.
        //            if (transaction.SpendingDetails != null && !transaction.SpendingDetails.IsSpentConfirmed() && idsToRemove.Contains(transaction.SpendingDetails.TransactionId))
        //            {
        //                result.Add((transaction.SpendingDetails.TransactionId, transaction.SpendingDetails.CreationTime));
        //                txs.ElementAt(i).SpendingDetails = null;
        //            }
        //        }
        //    }


        //    if (result.Any())
        //    {
        //        // Reload the lookup dictionaries.
        //        this.RefreshDictionary_OutpointTransactionData();

        //        this.SaveMetadata();
        //    }

        //    return result;
        //}

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate>();
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            throw new NotImplementedException();
        }
       

        #endregion

        #region private Methods

        P2WpkhAddress FindAddressByScriptPubKey(Script scriptPubKey)
        {
            byte[] raw = scriptPubKey.ToBytes();
            var pubKey = scriptPubKey.GetDestinationPublicKeys(this.network).SingleOrDefault();

            byte[] hash160;
            if (pubKey != null)
            {
                hash160 = pubKey.Hash.ToBytes();
            }
            else if (raw.Length == 22 && raw[0] == 0 && raw[1] == 20)  // push 20 (x14) bytes
            {
                hash160 = raw.Skip(2).Take(20).ToArray();
            }
            else
            {
                hash160 = scriptPubKey.Hash.ToBytes();
            }

            Debug.Assert(hash160.Length == 20);
            var key = hash160.ToHexString();
            this.X1WalletFile.P2WPKHAddresses.TryGetValue(key, out P2WpkhAddress address);
            return address;
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
        //void AddSpendingTransactionToWallet(Transaction transaction, IEnumerable<TxOut> paidToOutputs,
        //    uint256 spendingTransactionId, int? spendingTransactionIndex, int? blockHeight = null, Block block = null)
        //{
        //    Guard.NotNull(transaction, nameof(transaction));
        //    Guard.NotNull(paidToOutputs, nameof(paidToOutputs));

        //    uint256 transactionHash = transaction.GetHash();

        //    IEnumerable<TransactionData> allTransactionData = this.X1WalletFile.P2WPKHAddresses.Values.SelectMany(v => v.GetTransactionsByAddress());
        //    var spentTransaction = allTransactionData.SingleOrDefault(t => (t.Id == spendingTransactionId) && (t.Index == spendingTransactionIndex));

        //    if (spentTransaction == null)
        //    {
        //        // Strange, why would it be null?
        //        this.logger.LogTrace("(-)[TX_NULL]");
        //        return;
        //    }

        //    // If the details of this spending transaction are seen for the first time.
        //    if (spentTransaction.SpendingDetails == null)
        //    {
        //        this.logger.LogTrace("Spending UTXO '{0}-{1}' is new.", spendingTransactionId, spendingTransactionIndex);

        //        var payments = new List<PaymentDetails>();
        //        foreach (TxOut paidToOutput in paidToOutputs)
        //        {
        //            // Figure out how to retrieve the destination address.
        //            string destinationAddress = this.scriptAddressReader.GetAddressFromScriptPubKey(this.network, paidToOutput.ScriptPubKey);
        //            if (string.IsNullOrEmpty(destinationAddress))
        //            {
        //                var destination = FindAddressByScriptPubKey(paidToOutput.ScriptPubKey);
        //                if (destination != null)
        //                {
        //                    destinationAddress = destination.Address;
        //                }
        //            }


        //            payments.Add(new PaymentDetails
        //            {
        //                DestinationScriptPubKey = paidToOutput.ScriptPubKey,
        //                DestinationAddress = destinationAddress,
        //                Amount = paidToOutput.Value,
        //                OutputIndex = transaction.Outputs.IndexOf(paidToOutput)
        //            });
        //        }

        //        var spendingDetails = new SpendingDetails
        //        {
        //            TransactionId = transactionHash,
        //            Payments = payments,
        //            CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
        //            BlockHeight = blockHeight,
        //            BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
        //            Hex = transaction.ToHex(),
        //            IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true
        //        };

        //        spentTransaction.SpendingDetails = spendingDetails;
        //        spentTransaction.MerkleProof = null;
        //    }
        //    else // If this spending transaction is being confirmed in a block.
        //    {
        //        this.logger.LogTrace("Spending transaction ID '{0}' is being confirmed, updating.", spendingTransactionId);

        //        // Update the block height.
        //        if (spentTransaction.SpendingDetails.BlockHeight == null && blockHeight != null)
        //        {
        //            spentTransaction.SpendingDetails.BlockHeight = blockHeight;
        //        }

        //        // Update the block time to be that of the block in which the transaction is confirmed.
        //        if (block != null)
        //        {
        //            spentTransaction.SpendingDetails.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
        //            spentTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
        //        }
        //    }

        //    // If the transaction is spent and confirmed, we remove the UTXO from the lookup dictionary.
        //    if (spentTransaction.SpendingDetails.BlockHeight != null)
        //    {
        //        this.outpointLookup.Remove(new OutPoint(spentTransaction.Id, spentTransaction.Index));
        //    }
        //}

        void SaveMetadata()
        {
            this.Metadata.SaveX1WalletMetadataFile(this.CurrentX1WalletMetadataFilePath);
            this.logger.LogInformation("Wallet saved.");
        }




        void OnTransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
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
        /// Saves the tip and checkpoint from chainedHeader to the wallet file.
        /// </summary>
        /// <param name="lastBlockSynced">ChainedHeader of the last block synced.</param>
        void UpdateLastBlockSyncedAndCheckpoint(ChainedHeader lastBlockSynced)
        {
            this.Metadata.SyncedHeight = lastBlockSynced.Height;
            this.Metadata.SyncedHash = lastBlockSynced.HashBlock.ToString();

            const int minCheckpointHeight = 500;
            if (lastBlockSynced.Height > minCheckpointHeight)
            {
                var checkPoint = this.chainIndexer.GetHeader(lastBlockSynced.Height - minCheckpointHeight);
                this.Metadata.CheckpointHash = checkPoint.HashBlock.ToString();
                this.Metadata.CheckpointHeight = checkPoint.Height;
            }
            else
            {
                this.Metadata.CheckpointHash = this.network.GenesisHash.ToString();
                this.Metadata.CheckpointHeight = 0;
            }
        }

        internal P2WpkhAddress GetAddress(string address)
        {
            return this.X1WalletFile.P2WPKHAddresses.Values.Single(x => x.Address == address);
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
        //void AddTransactionToWallet(Transaction transaction, TxOut utxo, P2WpkhAddress address,  int? blockHeight = null, Block block = null, bool isPropagated = true)
        //{

        //    uint256 transactionHash = transaction.GetHash();

        //    // Get the collection of transactions to add to.
        //    Script script = utxo.ScriptPubKey;
        //    if (Metadata.Transactions.TryGetValue(address.HashHex, out List<TransactionData> txs))
        //    {
        //        // Check if a similar UTXO exists or not (same transaction ID and same index).
        //        // New UTXOs are added, existing ones are updated.
        //        int index = transaction.Outputs.IndexOf(utxo);
        //        Money amount = utxo.Value;
        //        TransactionData foundTransaction = txs.FirstOrDefault(t => (t.Id == transactionHash) && (t.Index == index));
        //        if (foundTransaction == null)
        //        {
        //            this.logger.LogTrace("UTXO '{0}-{1}' not found, creating.", transactionHash, index);
        //            var newTransaction = new TransactionData
        //            {
        //                Amount = amount,
        //                IsCoinBase = transaction.IsCoinBase == false ? (bool?)null : true,
        //                IsCoinStake = transaction.IsCoinStake == false ? (bool?)null : true,
        //                BlockHeight = blockHeight,
        //                BlockHash = block?.GetHash(),
        //                BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash),
        //                Id = transactionHash,
        //                CreationTime = DateTimeOffset.FromUnixTimeSeconds(block?.Header.Time ?? transaction.Time),
        //                Index = index,
        //                ScriptPubKey = script,
        //                Hex = transaction.ToHex(),
        //                IsPropagated = isPropagated,
        //            };

        //            // Add the Merkle proof to the (non-spending) transaction.
        //            if (block != null)
        //            {
        //                newTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
        //            }

        //            txs.Add(newTransaction);
        //            this.outpointLookup[new OutPoint(newTransaction.Id, newTransaction.Index)] = newTransaction;

        //            if (block == null)
        //            {
        //                // Unconfirmed inputs track for double spends.
        //                foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
        //                {
        //                    this.inputLookup[input] = newTransaction;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            this.logger.LogTrace("Transaction ID '{0}' found, updating.", transactionHash);

        //            // Update the block height and block hash.
        //            if ((foundTransaction.BlockHeight == null) && (blockHeight != null))
        //            {
        //                foundTransaction.BlockHeight = blockHeight;
        //                foundTransaction.BlockHash = block?.GetHash();
        //                foundTransaction.BlockIndex = block?.Transactions.FindIndex(t => t.GetHash() == transactionHash);
        //            }

        //            // Update the block time.
        //            if (block != null)
        //            {
        //                foundTransaction.CreationTime = DateTimeOffset.FromUnixTimeSeconds(block.Header.Time);
        //            }

        //            // Add the Merkle proof now that the transaction is confirmed in a block.
        //            if ((block != null) && (foundTransaction.MerkleProof == null))
        //            {
        //                foundTransaction.MerkleProof = new MerkleBlock(block, new[] { transactionHash }).PartialMerkleTree;
        //            }

        //            if (isPropagated)
        //                foundTransaction.IsPropagated = true;

        //            if (block != null)
        //            {
        //                // Inputs are in a block no need to track them anymore.
        //                foreach (OutPoint input in transaction.Inputs.Select(s => s.PrevOut))
        //                {
        //                    this.inputLookup.Remove(input);
        //                }
        //            }
        //        }


        //    }



        //}



        #endregion


    }
}
