using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;

namespace Obsidian.Features.X1Wallet
{
    public sealed class WalletSyncManagerState : IDisposable
    {
        readonly ISignals signals;
        readonly ILogger logger;
        public readonly IBlockStore BlockStore;
        public readonly StoreSettings StoreSettings;
        

        public ChainedHeader WalletTip;

        /// <summary>Queue which contains blocks that should be processed by <see cref="WalletManager"/>.</summary>
        readonly IAsyncDelegateDequeuer<Block> blockQueueEnqueuer;

        /// <summary>Current <see cref="blockQueueEnqueuer"/> size in bytes.</summary>
        public long BlocksQueueSize;

        /// <summary>Flag to determine when the <see cref="MaxQueueSize"/> is reached.</summary>
        bool maxQueueSizeReached;

        readonly SubscriptionToken blockConnectedSubscription;


        /// <summary>Limit <see cref="blockQueueEnqueuer"/> size to 100MB.</summary>
        const int MaxQueueSize = 100 * 1024 * 1024;

        public WalletSyncManagerState(ISignals signals, IBlockStore blockStore, StoreSettings storeSettings, ILogger logger, IAsyncDelegateDequeuer<Block> blocksQueueEnqueuer)
        {
            this.signals = signals;
            this.BlockStore = blockStore;
            this.StoreSettings = storeSettings;
            this.logger = logger;
            this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(OnBlockConnected);
            this.blockQueueEnqueuer = blocksQueueEnqueuer;
            // When a node is pruned it impossible to catch up
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be
            // able to download blocks from peers to catch up.
            if (storeSettings.PruningEnabled)
                throw new WalletException("Wallet can not yet run on a pruned node");
        }


        void OnBlockConnected(BlockConnected blockConnected)
        {
            if (!this.maxQueueSizeReached)
            {
                if (this.BlocksQueueSize >= MaxQueueSize)
                {
                    this.maxQueueSizeReached = true;
                    this.logger.LogTrace("(-)[REACHED_MAX_QUEUE_SIZE]");
                    return;
                }
            }
            else
            {
                // If queue is empty then reset the maxQueueSizeReached flag.
                this.maxQueueSizeReached = this.BlocksQueueSize > 0;
            }

            if (!this.maxQueueSizeReached)
            {
                var block = blockConnected.ConnectedBlock.Block;
                Debug.Assert(block.BlockSize != null, "block.BlockSize != null");
                long currentBlockQueueSize = Interlocked.Add(ref this.BlocksQueueSize, block.BlockSize.Value);
                this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

                this.blockQueueEnqueuer.Enqueue(block);
            }
        }

        public void Dispose()
        {
            this.blockQueueEnqueuer.Dispose();
            this.signals.Unsubscribe(this.blockConnectedSubscription);
            
        }
    }
}