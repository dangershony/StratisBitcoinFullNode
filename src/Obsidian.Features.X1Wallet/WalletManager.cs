﻿using System;
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
using static Obsidian.Features.X1Wallet.Extensions.Tools;

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
                this.X1WalletFile, ExpectedMetadataVersion, network.GenesisHash);

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

        public uint256 WalletLastBlockSyncedHash => this.Metadata.SyncedHash;

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
                isOnBestChain = this.chainIndexer.GetHeader(this.Metadata.SyncedHash) != null;
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
                var header = this.chainIndexer.GetHeader(this.Metadata.CheckpointHash);
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
            this.Metadata.SyncedHash = checkpointHeader.HashBlock;
            this.Metadata.CheckpointHeight = checkpointHeader.Height;
            this.Metadata.CheckpointHash = checkpointHeader.HashBlock;
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

                    this.X1WalletFile.Addresses.Add(address.HashHex, address);
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
                var addresses = this.X1WalletFile.Addresses.Values;
                header.AppendLine($"{this.X1WalletFile.Addresses.Count} found in wallet.");

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
            return this.X1WalletFile.Addresses;
        }

        /// <summary>
        /// Clears and initializes the wallet Metadata file, and sets heights to 0 and the hashes to null,
        /// and saves the Metadata file, effectively updating it to the latest version.
        /// </summary>
        internal void ResetMetadata()
        {
            this.Metadata.MetadataVersion = ExpectedMetadataVersion;
            this.Metadata.SyncedHash = this.network.GenesisHash;
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
                foreach (var tx in block.Value.Transactions)
                {
                    foreach (UtxoMetadata o in tx.Value.Received.Values)
                    {
                        coins.Add(new Coin(tx.Key, (uint)o.Index, Money.Satoshis(o.Satoshis), this.X1WalletFile.Addresses[o.HashHexAddress].GetScriptPubKey()));
                    }

                }
            }
            return res;
        }





        public P2WpkhAddress GetUnusedAddress()
        {
            foreach (P2WpkhAddress address in this.X1WalletFile.Addresses.Values)
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
                foreach (TransactionMetadata tx in block.Transactions.Values)
                {
                    foreach (var utxo in tx.Received.Values)
                    {
                        if (utxo.HashHexAddress == address.HashHex)
                            return true;
                    }

                }
            }
            return false;
        }






        public List<Coin> GetBudget(out Balance balance)
        {
            var receivedAndMature = new Dictionary<string, UtxoMetadata>();
            var spent = new Dictionary<string, UtxoMetadata>();

            long totalReceived = 0;
            long immatureReceived = 0;
            long totalSent = 0;


            foreach (var b in this.Metadata.Blocks)
            {
                var height = b.Key;
                var block = b.Value;

                foreach (var tx in block.Transactions.Values)
                {
                    bool isImmature = false;
                    if (tx.TxType == TxType.Coinbase)
                    {
                        var confirmations = this.Metadata.SyncedHeight - height + 1;
                        isImmature = confirmations < this.network.Consensus.CoinbaseMaturity;
                    }

                    if (tx.Received != null)
                    {
                        foreach (var r in tx.Received)
                        {
                            totalReceived += r.Value.Satoshis;

                            if (!isImmature)
                            {
                                receivedAndMature.Add(r.Key, r.Value);
                            }
                            else
                            {
                                immatureReceived += r.Value.Satoshis;
                            }
                        }
                    }
                    if (tx.Spent != null)
                    {
                        foreach (var s in tx.Spent)
                        {
                            totalSent += s.Value.Satoshis;
                            spent.Add(s.Key, s.Value);

                        }
                    }

                }




            }

            foreach (var item in spent)
            {
                if (receivedAndMature.ContainsKey(item.Key))
                    receivedAndMature.Remove(item.Key);
            }

            var coins = new List<Coin>();
            foreach (var utxo in receivedAndMature.Values)
                coins.Add(new Coin(new uint256(utxo.HashTx), (uint)utxo.Index, Money.Satoshis(utxo.Satoshis), this.X1WalletFile.Addresses[utxo.HashHexAddress].GetScriptPubKey()));

            balance = new Balance
            {
                AmountConfirmed = Money.Satoshis(totalReceived - totalSent),
                AmountUnconfirmed = Money.Zero,
                SpendableAmount = Money.Satoshis(totalReceived - totalSent - immatureReceived) // todo: unconfirmed
            };

            return coins;
        }



        void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {

            foreach (Transaction transaction in block.Transactions)
            {
                var received = ProcessTransaction(transaction, chainedHeader.Height, block);
                if (received.HasValue)
                    this.logger.LogInformation($"Transaction {transaction.GetHash()} in block {chainedHeader.Height} added {Money.Satoshis(received.Value)} {this.network.CoinTicker} to the wallet.");
            }

            UpdateLastBlockSyncedAndCheckpoint(chainedHeader);

            if (!this.isStartingUp)
                SaveMetadata();
        }

        Dictionary<string, UtxoMetadata> ExtractIncomingFunds(Transaction transaction, out long amountReceived)
        {
            Dictionary<string, UtxoMetadata> received = null;
            long sum = 0;
            int index = 0;

            foreach (var output in transaction.Outputs)
            {
                P2WpkhAddress ownAddress = FindAddressByScriptPubKey(output.ScriptPubKey);
                if (ownAddress != null)
                {
                    NotNull(ref received, transaction.Outputs.Count);

                    var item = new UtxoMetadata
                    {
                        HashHexAddress = ownAddress.HashHex,
                        HashTx = transaction.GetHash(),
                        Satoshis = output.Value.Satoshi,
                        Index = index
                    };
                    received.Add(item.GetKey(), item);
                    sum += item.Satoshis;
                }
                index++;
            }
            amountReceived = sum;
            return received;
        }

        Dictionary<string, UtxoMetadata> ExtractOutgoingFunds(Transaction transaction, out long amountSpent)
        {
            if (transaction.IsCoinStake || transaction.IsCoinBase)
            {
                amountSpent = 0;
                return null;
            }

            List<OutPoint> prevOuts = GetPrevOuts(transaction);
            Dictionary<string, UtxoMetadata> spends = null;
            long sum = 0;

            foreach (var b in this.Metadata.Blocks.Values) // iterate ovr the large collection in outer loop (only once)
            {
                findOutPointInBlock:
                foreach (OutPoint prevOut in prevOuts)
                {
                    if (b.Transactions.TryGetValue(prevOut.Hash, out TransactionMetadata prevTx))  // prevOut tx id is in the wallet
                    {
                        var prevWalletUtxo = prevTx.Received.Values.SingleOrDefault(x => x.Index == prevOut.N);  // do we have the indexed output?
                        if (prevWalletUtxo != null)  // yes, it's a spend from this wallet
                        {
                            NotNull(ref spends, transaction.Inputs.Count); // ensure the return collection is initialized
                            spends.Add(prevWalletUtxo.GetKey(), prevWalletUtxo);  // add the spend
                            sum += prevWalletUtxo.Satoshis; // add amount

                            if (spends.Count == transaction.Inputs.Count) // we will find no more spends than inputs, quick exit
                            {
                                amountSpent = sum;
                                return spends;
                            }

                            prevOuts.Remove(prevOut); // do not search for this item any more
                            goto findOutPointInBlock; // we need a new enumerator for the shortened collection
                        }
                    }  // is the next prvOut also in this block? That's definitely possible!
                }
            }
            amountSpent = sum;
            return spends; // might be null or contain less then the tx inputs in edge cases, e.g. if an private key was removed from the wallet and no more items than the tx inputs.
        }

        List<OutPoint> GetPrevOuts(Transaction transaction)
        {
            var prevOuts = new List<OutPoint>(transaction.Inputs.Count);
            foreach (TxIn input in transaction.Inputs)
            {
                prevOuts.Add(input.PrevOut);
            }

            return prevOuts;
        }



        long? ProcessTransaction(Transaction transaction, int blockHeight, Block block)
        {

            var received = ExtractIncomingFunds(transaction, out var amountReceived);
            var spent = ExtractOutgoingFunds(transaction, out var amountSpent);

            if (received == null && spent == null)
                return null;

            var walletTransaction = new TransactionMetadata
            {
                TxType = GetTxType(transaction, received, spent),
                HashTx = transaction.GetHash(),
                Received = received,
                Spent = spent,
            };

            if (!this.Metadata.Blocks.TryGetValue(blockHeight, out BlockMetadata walletBlock))
            {
                walletBlock = new BlockMetadata { HashBlock = block.GetHash(), Transactions = new Dictionary<uint256, TransactionMetadata>() };
                this.Metadata.Blocks.Add(blockHeight, walletBlock);
            }

            walletBlock.Transactions.Add(walletTransaction.HashTx, walletTransaction);


            return amountReceived - amountSpent;

        }

        TxType GetTxType(Transaction transaction, Dictionary<string, UtxoMetadata> received, Dictionary<string, UtxoMetadata> spent)
        {
            if (transaction.IsCoinBase)
                return TxType.Coinbase;
            if (transaction.IsCoinStake)
            {
                if (transaction.Outputs.Count == 2)
                    return TxType.CoinstakeLegacy;
                if (transaction.Outputs.Count == 3)
                    return TxType.Coinstake;
            }

            if (received != null && received.Count > 0)
            {
                if (spent == null || spent.Count == 0)
                    return TxType.Receive;
                else
                    return TxType.SpendReceive;
            }
            else
            {
                if (spent != null && spent.Count > 0)
                    return TxType.Spend;
            }

            throw new ArgumentException(
                $"{nameof(GetTxType)} cant't determine {nameof(TxType)} for transaction {transaction.GetHash()}.");
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
            this.X1WalletFile.Addresses.TryGetValue(key, out P2WpkhAddress address);
            return address;
        }




      

        void SaveMetadata()
        {
            this.Metadata.SaveX1WalletMetadataFile(this.CurrentX1WalletMetadataFilePath);
            this.logger.LogInformation("Wallet saved.");
        }




        void OnTransactionStateChanged(object sender, TransactionBroadcastEntry transactionEntry)
        {
            if (string.IsNullOrEmpty(transactionEntry.ErrorMessage))
            {
                // TODO: process unconfirmed tx separately, do not put them in the wallet file
                this.logger.LogWarning("X1Wallet.WalletManager: Processing mempool tx is not yet implemented!");
                //this.ProcessTransaction(transactionEntry.Transaction, null, null, transactionEntry.State == State.Propagated);
            }
            else
            {
                this.logger.LogWarning("Exception occurred: {0}", transactionEntry.ErrorMessage);
            }
        }

        /// <summary>
        /// Saves the tip and checkpoint from chainedHeader to the wallet file.
        /// </summary>
        /// <param name="lastBlockSynced">ChainedHeader of the last block synced.</param>
        void UpdateLastBlockSyncedAndCheckpoint(ChainedHeader lastBlockSynced)
        {
            this.Metadata.SyncedHeight = lastBlockSynced.Height;
            this.Metadata.SyncedHash = lastBlockSynced.HashBlock;

            const int minCheckpointHeight = 500;
            if (lastBlockSynced.Height > minCheckpointHeight)
            {
                var checkPoint = this.chainIndexer.GetHeader(lastBlockSynced.Height - minCheckpointHeight);
                this.Metadata.CheckpointHash = checkPoint.HashBlock;
                this.Metadata.CheckpointHeight = checkPoint.Height;
            }
            else
            {
                this.Metadata.CheckpointHash = this.network.GenesisHash;
                this.Metadata.CheckpointHeight = 0;
            }
        }

        internal P2WpkhAddress GetAddress(string address)
        {
            return this.X1WalletFile.Addresses.Values.Single(x => x.Address == address);
        }

    

        #endregion


    }
}
