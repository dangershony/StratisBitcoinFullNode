using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Temp;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    public class WalletManagerWrapper : IDisposable
    {
        readonly object lockObject = new object();
        readonly DataFolder dataFolder;
        readonly ChainIndexer chainIndexer;
        public readonly Network network;
        readonly IBroadcasterManager broadcasterManager;
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;
        readonly IScriptAddressReader scriptAddressReader;
        readonly IDateTimeProvider dateTimeProvider;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;

        // for wallet syncing
        readonly ISignals signals;
        readonly IBlockStore blockStore;
        readonly StoreSettings storeSettings;

        // for staking
        // for staking
        readonly IPosMinting posMinting;
        readonly ITimeSyncBehaviorState timeSyncBehaviorState;
        readonly IWalletManagerStakingAdapter walletManagerStakingAdapter;

        static readonly RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

        WalletManager walletManager;

        public WalletManagerWrapper(DataFolder dataFolder, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory,
            IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider, ISignals signals, IBlockStore blockStore, StoreSettings storeSettings, IPosMinting posMinting, ITimeSyncBehaviorState timeSyncBehaviorState, IWalletManager walletManagerStakingAdapter)
        {
            this.dataFolder = dataFolder;
            this.chainIndexer = chainIndexer;
            this.network = network;
            this.broadcasterManager = broadcasterManager;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(typeof(WalletManagerWrapper).FullName);
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            this.signals = signals;
            this.blockStore = blockStore;
            this.storeSettings = storeSettings;

            this.posMinting = posMinting;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.walletManagerStakingAdapter = (IWalletManagerStakingAdapter)walletManagerStakingAdapter;

        }

        public WalletContext GetWalletContext(string walletName, bool doNotCheck = false)
        {
            if (doNotCheck)
            {
                if (this.walletManager == null)
                    return null;
                return new WalletContext(this.walletManager);
            }

            if (walletName == null)
                throw new ArgumentNullException(nameof(walletName));

            if (this.walletManager != null)
            {
                if (this.walletManager.WalletName == walletName)
                    return new WalletContext(this.walletManager);
                throw new InvalidOperationException($"Invalid request for wallet {walletName} - the current wallet is {this.walletManager.WalletName}");
            }
            lock (this.lockObject)
            {
                if (this.walletManager == null)
                {
                    LoadWallet(walletName).GetAwaiter().GetResult();
                    Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
                    WalletSyncManagerStart();
                    this.walletManagerStakingAdapter.SetWalletManagerWrapper(this, walletName);
                }
            }
            return new WalletContext(this.walletManager);

        }

        WalletContext GetWalletContextPrivate()
        {
            return GetWalletContext(null, true);
        }

        public async Task LoadWallet(string walletName)
        {
            string x1WalletFilePath = walletName.GetX1WalletFilepath(this.network, this.dataFolder);
           
            if (!File.Exists(x1WalletFilePath))
                throw new FileNotFoundException($"No wallet file found at {x1WalletFilePath}");


            if (this.walletManager != null)
            {
                if (this.walletManager.CurrentX1WalletFilePath != x1WalletFilePath)
                    throw new NotSupportedException(
                        "Core wallet manager already created, changing the wallet file while node and wallet are running is not currently supported.");
            }
            this.walletManager = new WalletManager(x1WalletFilePath, this.chainIndexer, this.network, this.dataFolder, this.broadcasterManager, this.loggerFactory, this.scriptAddressReader, this.dateTimeProvider, this.nodeLifetime, this.asyncProvider, this.posMinting, this.timeSyncBehaviorState);
        }

        public async Task CreateKeyWalletAsync(WalletCreateRequest walletCreateRequest)
        {
            string walletName = walletCreateRequest.Name;
            string filePath = walletName.GetX1WalletFilepath(this.network, this.dataFolder);

            if (File.Exists(filePath))
                throw new InvalidOperationException($"A wallet with the name {walletName} already exists at {filePath}!");

            if (string.IsNullOrWhiteSpace(walletCreateRequest.Password))
                throw new InvalidOperationException("A passphrase is required.");

            var x1WalletFile = new X1WalletFile { P2WPKHAddresses = new Dictionary<string, P2WPKHAddress>() };

            x1WalletFile.WalletGuid = Guid.NewGuid();
            x1WalletFile.WalletName = walletName;

            // Create the passphrase challenge
            var challengePlaintext = new byte[32];
            rng.GetBytes(challengePlaintext);
            x1WalletFile.PassphraseChallenge = VCL.EncryptWithPassphrase(walletCreateRequest.Password, challengePlaintext);

            x1WalletFile.Comment = $"Created on {DateTime.UtcNow} with filename {filePath}";
            x1WalletFile.Version = 1;

            const int witnessVersion = 0;
            var bech32Prefix = this.network.CoinTicker.ToLowerInvariant();  // https://github.com/bitcoin/bips/blob/master/bip-0173.mediawiki

            const int addressPoolSize = 1000;
            for (var i = 0; i < addressPoolSize; i++)
            {
                var bytes = new byte[32];
                rng.GetBytes(bytes);
                var isChange = i % 4 != 0; // 75% change addresses
                var address = P2WPKHAddress.CreateWithPrivateKey(bytes, walletCreateRequest.Password, VCL.EncryptWithPassphrase, isChange, this.network.Consensus.CoinType, witnessVersion, bech32Prefix, this.network.CoinTicker);
                x1WalletFile.P2WPKHAddresses.Add(address.HashHex, address);
            }
            if (x1WalletFile.P2WPKHAddresses.Count != addressPoolSize)
                throw new Exception("Something is seriously wrong, collision of random numbers detected. Do not use this wallet.");

            x1WalletFile.SaveX1WalletFile(filePath);

            X1WalletMetadataFile x1WalletMetadataFile = x1WalletFile.CreateX1WalletMetadataFile();
            var x1WalletMetadataFilename = walletName.GetX1WalletMetaDataFilepath(this.network, this.dataFolder);
            x1WalletMetadataFile.SaveX1WalletMetadataFile(x1WalletMetadataFilename);

        }

        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            var filePathes = Directory.EnumerateFiles(this.dataFolder.WalletPath, $"*{X1WalletFile.FileExtension}", SearchOption.TopDirectoryOnly);
            var files = filePathes.Select(Path.GetFileName);
            return (this.dataFolder.WalletPath, files);
        }

        #region IWalletSyncManager

        WalletSyncManagerState syncState;
        SubscriptionToken transactionReceivedSubscription;

        void WalletSyncManagerStart()
        {
            IAsyncDelegateDequeuer<Block> blocksQueue = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<Block>("WalletSyncMangerStateBlocksQueue",
                OnProcessBlockAsync);
            this.syncState = new WalletSyncManagerState(this.signals, this.blockStore, this.storeSettings, this.logger, blocksQueue);



            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.", this.walletManager.WalletLastBlockSyncedHeight);

            //using (var context = GetWalletContextPrivate())
            //{
            //    this.syncState.WalletTip = this.chainIndexer.GetHeader(context.WalletManager.WalletLastBlockSyncedHash);
            //    if (this.syncState.WalletTip == null)
            //    {
            //        // The wallet tip was not found in the main chain.
            //        // this can happen if the node crashes unexpectedly.
            //        // To recover we need to find the first common fork
            //        // with the best chain. As the wallet does not have a
            //        // list of chain headers, we use a BlockLocator and persist
            //        // that in the wallet. The block locator will help finding
            //        // a common fork and bringing the wallet back to a good
            //        // state (behind the best chain).
            //        ICollection<uint256> locators = context.WalletManager.WalletBlockLocator;
            //        var blockLocator = new BlockLocator { Blocks = locators.ToList() };
            //        ChainedHeader fork = this.chainIndexer.FindFork(blockLocator);
            //        context.WalletManager.RemoveBlocks(fork);
            //        //this.WalletTipHash = fork.HashBlock;
            //        //this.WalletTipHeight = fork.Height;
            //        this.syncState.WalletTip = fork;
            //    }
            //}


            this.transactionReceivedSubscription = this.signals.Subscribe<TransactionReceived>(async (args) => await OnMemoryPoolNewTransactionFromPeerAvailableAsync(args));
        }

        /// <summary>Called when a <see cref="Block"/> is added to the <see cref="WalletSyncManagerState.blockQueueEnqueuer"/>.
        /// Depending on the <see cref="WalletSyncManagerState.WalletTip"/> and incoming block height, this method will decide whether the <see cref="Block"/>
        /// will be processed by the <see cref="WalletManager"/>.
        /// </summary>
        /// <param name="block">Block to be processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        async Task OnProcessBlockAsync(Block block, CancellationToken cancellationToken)
        {
            Guard.NotNull(block, nameof(block));
            Debug.Assert(block.BlockSize != null, "block.BlockSize != null");

            long currentBlockQueueSize =
                Interlocked.Add(ref this.syncState.BlocksQueueSize, -block.BlockSize.Value);
            this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

            ChainedHeader newTip = this.chainIndexer.GetHeader(block.GetHash());

            if (newTip == null)
            {
                this.logger.LogTrace("(-)[NEW_TIP_REORG]");
                return;
            }
        retry:
            if (this.syncState.WalletTip == null)
            {
                using (var context = GetWalletContextPrivate())
                {
                    this.syncState.WalletTip = this.chainIndexer.GetHeader(context.WalletManager.WalletLastBlockSyncedHash);
                    await Task.Delay(250);
                    goto retry;

                }
            }

            // If the new block's previous hash is not the same as the one we have, there might have been a reorg.
            // If the new block follows the previous one, just pass the block to the manager.
            if (block.Header.HashPrevBlock != this.syncState.WalletTip.HashBlock)
            {
                // If previous block does not match there might have
                // been a reorg, check if the wallet is still on the main chain.
                ChainedHeader inBestChain = this.chainIndexer.GetHeader(this.syncState.WalletTip.HashBlock);
                if (inBestChain == null)
                {
                    // The current wallet hash was not found on the main chain.
                    // A reorg happened so bring the wallet back top the last known fork.
                    ChainedHeader fork = this.syncState.WalletTip;

                    // We walk back the chained block object to find the fork.
                    while (this.chainIndexer.GetHeader(fork.HashBlock) == null)
                        fork = fork.Previous;

                    this.logger.LogInformation("Reorg detected, going back from '{0}' to '{1}'.",
                        this.syncState.WalletTip, fork);


                    using (var context = GetWalletContextPrivate())
                    {
                        context.WalletManager.RemoveBlocks(fork);
                    }


                    this.syncState.WalletTip = fork;

                    this.logger.LogTrace("Wallet tip set to '{0}'.", this.syncState.WalletTip);
                }

                // The new tip can be ahead or behind the wallet.
                // If the new tip is ahead we try to bring the wallet up to the new tip.
                // If the new tip is behind we just check the wallet and the tip are in the same chain.
                if (newTip.Height > this.syncState.WalletTip.Height)
                {
                    ChainedHeader findTip = newTip.FindAncestorOrSelf(this.syncState.WalletTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_AHEAD_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogTrace("Wallet tip '{0}' is behind the new tip '{1}'.", this.syncState.WalletTip,
                        newTip);

                    ChainedHeader next = this.syncState.WalletTip;
                    while (next != newTip)
                    {
                        // While the wallet is catching up the entire node will wait.
                        // If a wallet is recovered to a date in the past. Consensus will stop until the wallet is up to date.

                        // TODO: This code should be replaced with a different approach
                        // Similar to BlockStore the wallet should be standalone and not depend on consensus.
                        // The block should be put in a queue and pushed to the wallet in an async way.
                        // If the wallet is behind it will just read blocks from store (or download in case of a pruned node).

                        next = newTip.GetAncestor(next.Height + 1);
                        Block nextBlock;
                        int index = 0;
                        while (true)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                this.logger.LogTrace("(-)[CANCELLATION_REQUESTED]");
                                return;
                            }

                            nextBlock = this.syncState.BlockStore.GetBlock(next.HashBlock);
                            if (nextBlock == null)
                            {
                                // The idea in this abandoning of the loop is to release consensus to push the block.
                                // That will make the block available in the next push from consensus.
                                index++;
                                if (index > 10)
                                {
                                    this.logger.LogTrace("(-)[WALLET_CATCHUP_INDEX_MAX]");
                                    return;
                                }

                                // Really ugly hack to let store catch up.
                                // This will block the entire consensus pulling.
                                this.logger.LogWarning(
                                    "Wallet is behind the best chain and the next block is not found in store.");
                                Thread.Sleep(100);
                                continue;
                            }

                            break;
                        }

                        this.syncState.WalletTip = next;

                        using (var context = GetWalletContextPrivate())
                        {
                            context.WalletManager.ProcessBlock(nextBlock, next);
                        }

                    }
                }
                else
                {
                    ChainedHeader findTip = this.syncState.WalletTip.FindAncestorOrSelf(newTip);
                    if (findTip == null)
                    {
                        this.logger.LogTrace("(-)[NEW_TIP_BEHIND_NOT_IN_WALLET]");
                        return;
                    }

                    this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.",
                        this.syncState.WalletTip, newTip);
                }
            }
            else
                this.logger.LogTrace("New block follows the previously known block '{0}'.",
                    this.syncState.WalletTip);

            this.syncState.WalletTip = newTip;

            using (var context = GetWalletContextPrivate())
            {
                context.WalletManager.ProcessBlock(block, newTip);
            }
        }

        async Task OnMemoryPoolNewTransactionFromPeerAvailableAsync(TransactionReceived transactionReceived)
        {
            using (var context = GetWalletContextPrivate())
            {
                context.WalletManager.ProcessTransaction(transactionReceived.ReceivedTransaction);
            }
        }

        public async Task WalletSyncManagerSyncFromDateAsync(DateTime date)
        {
            int blockSyncStart = this.chainIndexer.GetHeightAtTime(date);
            await WalletSyncManagerSyncFromHeightAsync(blockSyncStart);
        }

        public async Task WalletSyncManagerSyncFromHeightAsync(int height)
        {
            ChainedHeader chainedHeader = this.chainIndexer.GetHeader(height);
            if (chainedHeader == null)
                throw new WalletException("Invalid block height");

            using (var context = GetWalletContextPrivate())
            {
                context.WalletManager.RemoveBlocks(chainedHeader);
            }

            this.syncState.WalletTip = chainedHeader;
        }

        public void Dispose()
        {
            this.syncState?.Dispose();
            this.signals.Unsubscribe(this.transactionReceivedSubscription);

        }

        #endregion





    }
}
