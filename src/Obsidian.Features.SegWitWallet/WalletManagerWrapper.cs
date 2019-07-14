using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Temp;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
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
        readonly DataFolder dataFolder;
        readonly ChainIndexer chainIndexer;
        readonly Network network;
        readonly IBroadcasterManager broadcasterManager;
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;
        readonly IScriptAddressReader scriptAddressReader;
        readonly IDateTimeProvider dateTimeProvider;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;

        readonly ISignals signals;
        readonly IBlockStore blockStore;
        readonly StoreSettings storeSettings;

        WalletManager walletManager;

        public WalletManagerWrapper(DataFolder dataFolder, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory,
            IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider, ISignals signals, IBlockStore blockStore, StoreSettings storeSettings)
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
        }

        public WalletContext GetWalletContext(string walletName, bool doNotCheck = false)
        {
            if (doNotCheck)
                return new WalletContext(this.walletManager);

            if (walletName == null)
                throw new ArgumentNullException(nameof(walletName));

            if (this.walletManager != null)
            {
                if (this.walletManager.WalletName == walletName)
                    return new WalletContext(this.walletManager);
                throw new InvalidOperationException($"Invalid request for wallet {walletName} - the current wallet is {this.walletManager.WalletName}");
            }

            LoadWallet(walletName).GetAwaiter().GetResult();
            Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
            WalletSyncManagerStart();
            return GetWalletContext(walletName);
        }

        public async Task LoadWallet(string name)
        {


            var fileName = $"{name}{WalletManager.WalletFileExtension}";
            string filePath = Path.Combine(this.dataFolder.WalletPath, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"No wallet file found at {filePath}");
            if (this.walletManager != null)
            {
                if (this.walletManager.CurrentWalletFilePath != filePath)
                    throw new NotSupportedException(
                        "Core wallet manager already created, changing the wallet file while node and wallet are running is not currently supported.");
            }
            this.walletManager = new WalletManager(filePath, this.chainIndexer, this.network, this.broadcasterManager, this.loggerFactory, this.scriptAddressReader, this.dateTimeProvider, this.nodeLifetime, this.asyncProvider);
        }

        public async Task CreateKeyWalletAsync(WalletCreateRequest walletCreateRequest)
        {
            var fileName = $"{walletCreateRequest.Name}{WalletManager.WalletFileExtension}";
            string filePath = Path.Combine(this.dataFolder.WalletPath, fileName);
            if (File.Exists(filePath))
                throw new InvalidOperationException($"A wallet with the name {walletCreateRequest.Name} already exists at {filePath}!");

            if (walletCreateRequest.Password == null || walletCreateRequest.Password.Length < 12)
                throw new InvalidOperationException("A passphrase with at least 12 characters is required.");

            var keyWallet = new KeyWallet
            {
                Name = walletCreateRequest.Name,
                CreationTime = DateTime.UtcNow,
                WalletType = nameof(KeyWallet),
                WalletTypeVersion = 1,
                Addresses = new Dictionary<string, KeyAddress>(),
                LastBlockSyncedHash = this.network.Consensus.HashGenesisBlock,
                LastBlockSyncedHeight = 0
            };
            const int witnessVersion = 0;
            var bech32Prefix = "odx";  // https://github.com/bitcoin/bips/blob/master/bip-0173.mediawiki



            int start = 23;
            int end = 43;
            for (var i = start; i <= end; i++)
            {
                var bytes = new byte[32];
                StaticWallet.Fill((byte)i, bytes);
                var isChange = i % 2 == 0;
                var address = KeyAddress.CreateWithPrivateKey(bytes, walletCreateRequest.Password, VCL.EncryptWithPassphrase, this.network.Consensus.CoinType, witnessVersion, bech32Prefix);
                address.IsChange = isChange;
                keyWallet.Addresses.Add(address.ScriptPubKey.ToHex(), address);
            }

            var serializedWallet = JsonConvert.SerializeObject(keyWallet, Formatting.Indented);
            File.WriteAllText(filePath, serializedWallet);
            await Task.CompletedTask;

        }

        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            var filePathes = Directory.EnumerateFiles(this.dataFolder.WalletPath, $"*{WalletManager.WalletFileExtension}", SearchOption.TopDirectoryOnly);
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

            Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
            this.syncState.WalletTip = this.chainIndexer.GetHeader(this.walletManager.WalletLastBlockSyncedHash);
            if (this.syncState.WalletTip == null)
            {
                // The wallet tip was not found in the main chain.
                // this can happen if the node crashes unexpectedly.
                // To recover we need to find the first common fork
                // with the best chain. As the wallet does not have a
                // list of chain headers, we use a BlockLocator and persist
                // that in the wallet. The block locator will help finding
                // a common fork and bringing the wallet back to a good
                // state (behind the best chain).
                ICollection<uint256> locators = this.walletManager.WalletBlockLocator;
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chainIndexer.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                //this.WalletTipHash = fork.HashBlock;
                //this.WalletTipHeight = fork.Height;
                this.syncState.WalletTip = fork;
            }

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

                    try
                    {
                        await this.walletManager.WalletSemaphore.WaitAsync(cancellationToken);
                        this.walletManager.RemoveBlocks(fork);
                    }
                    finally
                    {
                        this.walletManager.WalletSemaphore.Release();
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

                        try
                        {
                            await this.walletManager.WalletSemaphore.WaitAsync(cancellationToken);
                            this.walletManager.ProcessBlock(nextBlock, next);
                        }
                        finally
                        {
                            this.walletManager.WalletSemaphore.Release();
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
            try
            {
                await this.walletManager.WalletSemaphore.WaitAsync(cancellationToken);
                this.walletManager.ProcessBlock(block, newTip);
            }
            finally
            {
                this.walletManager.WalletSemaphore.Release();
            }
        }

        async Task OnMemoryPoolNewTransactionFromPeerAvailableAsync(TransactionReceived transactionReceived)
        {
            try
            {
                await this.walletManager.WalletSemaphore.WaitAsync();
                this.walletManager.ProcessTransaction(transactionReceived.ReceivedTransaction);
            }
            finally
            {
                this.walletManager.WalletSemaphore.Release();
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

            try
            {
                await this.walletManager.WalletSemaphore.WaitAsync();
                this.walletManager.RemoveBlocks(chainedHeader);
            }
            finally
            {
                this.walletManager.WalletSemaphore.Release();
            }

            this.syncState.WalletTip = chainedHeader;
        }

        public void Dispose()
        {
            this.syncState.Dispose();
            this.signals.Unsubscribe(this.transactionReceivedSubscription);

        }

        #endregion





    }
}
