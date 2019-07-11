using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet
{
    public partial class WalletManagerWrapper : IWalletSyncManager, IDisposable
    {
        WalletSyncManagerState syncState;
        ChainedHeader IWalletSyncManager.WalletTip => throw new NotImplementedException();

        private void ConstructWalletSyncManager(ISignals signals, IBlockStore blockStore, StoreSettings storeSettings)
        {
            this.syncState = new WalletSyncManagerState(signals, blockStore, storeSettings)
            {
                BlocksQueue = this.asyncProvider.CreateAndRunAsyncDelegateDequeuer<Block>("WalletSyncMangerStateBlocksQueue", OnProcessBlockAsync)
            };
        }


        void IWalletSyncManager.Start()
        {
            // When a node is pruned it impossible to catch up
            // if the wallet falls behind the block puller.
            // To support pruning the wallet will need to be
            // able to download blocks from peers to catch up.
            if (this.syncState.StoreSettings.PruningEnabled)
                throw new WalletException("Wallet can not yet run on a pruned node");

            this.logger.LogInformation("WalletSyncManager initialized. Wallet at block {0}.",  this.walletManager.Wallet.LastBlockSyncedHeight);

            Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
            this.syncState.WalletTip = this.chainIndexer.GetHeader(this.walletManager.Wallet.LastBlockSyncedHash);
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
                ICollection<uint256> locators =  this.walletManager.Wallet.BlockLocator;
                var blockLocator = new BlockLocator { Blocks = locators.ToList() };
                ChainedHeader fork = this.chainIndexer.FindFork(blockLocator);
                this.walletManager.RemoveBlocks(fork);
                //this.WalletTipHash = fork.HashBlock;
                //this.WalletTipHeight = fork.Height;
                this.syncState.WalletTip = fork;
            }

            this.syncState.BlockConnectedSubscription = this.syncState.Signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            this.syncState.TransactionReceivedSubscription = this.syncState.Signals.Subscribe<TransactionReceived>(this.OnTransactionAvailable);
        }

        private void OnTransactionAvailable(TransactionReceived transactionReceived)
        {
            ((IWalletSyncManager)this).ProcessTransaction(transactionReceived.ReceivedTransaction);
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            ((IWalletSyncManager)this).ProcessBlock(blockConnected.ConnectedBlock.Block);
        }

        void IWalletSyncManager.Stop()
        {
            this.syncState.Signals.Unsubscribe(this.syncState.BlockConnectedSubscription);
            this.syncState.Signals.Unsubscribe(this.syncState.TransactionReceivedSubscription);
        }

        void IWalletSyncManager.ProcessBlock(Block block)
        {
            Guard.NotNull(block, nameof(block));

            Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
            //if (!this.ContainsWallets)
            //{
            //    this.logger.LogTrace("(-)[NO_WALLET]");
            //    return;
            //}

            // If the queue reaches the maximum limit, ignore incoming blocks until the queue is empty.
            if (!this.syncState.MaxQueueSizeReached)
            {
                if (this.syncState.BlocksQueueSize >= WalletSyncManagerState.MaxQueueSize)
                {
                    this.syncState.MaxQueueSizeReached = true;
                    this.logger.LogTrace("(-)[REACHED_MAX_QUEUE_SIZE]");
                    return;
                }
            }
            else
            {
                // If queue is empty then reset the maxQueueSizeReached flag.
                this.syncState.MaxQueueSizeReached = this.syncState.BlocksQueueSize > 0;
            }

            if (!this.syncState.MaxQueueSizeReached)
            {
                long currentBlockQueueSize = Interlocked.Add(ref this.syncState.BlocksQueueSize, block.BlockSize.Value);
                this.logger.LogTrace("Queue sized changed to {0} bytes.", currentBlockQueueSize);

                this.syncState.BlocksQueue.Enqueue(block);
            }
        }

        void IWalletSyncManager.ProcessTransaction(Transaction transaction)
        {
            Guard.NotNull(transaction, nameof(transaction));

            this.ProcessTransaction(transaction); // yes, calls IWalletManager.ProcessTransaction
        }

        void IWalletSyncManager.SyncFromDate(DateTime date)
        {
            int blockSyncStart = this.chainIndexer.GetHeightAtTime(date);
            ((IWalletSyncManager)this).SyncFromHeight(blockSyncStart);
        }

        void IWalletSyncManager.SyncFromHeight(int height)
        {
            ChainedHeader chainedHeader = this.chainIndexer.GetHeader(height);
            if(chainedHeader == null)
                throw  new WalletException("Invalid block height");

            this.walletManager.RemoveBlocks(chainedHeader);
           
            //this.WalletTipHash = chainedHeader.HashBlock;
            //this.WalletTipHeight = chainedHeader.Height;
            this.syncState.WalletTip = chainedHeader;
        }



        /// <summary>Called when a <see cref="Block"/> is added to the <see cref="blocksQueue"/>.
        /// Depending on the <see cref="WalletTip"/> and incoming block height, this method will decide whether the <see cref="Block"/> will be processed by the <see cref="WalletManager"/>.
        /// </summary>
        /// <param name="block">Block to be processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task OnProcessBlockAsync(Block block, CancellationToken cancellationToken)
        {
            Guard.NotNull(block, nameof(block));

            long currentBlockQueueSize = Interlocked.Add(ref this.syncState.BlocksQueueSize, -block.BlockSize.Value);
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

                    this.logger.LogInformation("Reorg detected, going back from '{0}' to '{1}'.", this.syncState.WalletTip, fork);

                    this.walletManager.RemoveBlocks(fork);
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

                    this.logger.LogTrace("Wallet tip '{0}' is behind the new tip '{1}'.", this.syncState.WalletTip, newTip);

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
                        Block nextblock = null;
                        int index = 0;
                        while (true)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                this.logger.LogTrace("(-)[CANCELLATION_REQUESTED]");
                                return;
                            }

                            nextblock = this.syncState.BlockStore.GetBlock(next.HashBlock);
                            if (nextblock == null)
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
                                this.logger.LogWarning("Wallet is behind the best chain and the next block is not found in store.");
                                Thread.Sleep(100);
                                continue;
                            }

                            break;
                        }

                        this.syncState.WalletTip = next;

                        ProcessBlock(nextblock, next);
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

                    this.logger.LogTrace("Wallet tip '{0}' is ahead or equal to the new tip '{1}'.", this.syncState.WalletTip, newTip);
                }
            }
            else this.logger.LogTrace("New block follows the previously known block '{0}'.", this.syncState.WalletTip);

            this.syncState.WalletTip = newTip;
            ProcessBlock(block, newTip);
        }

        public void Dispose()
        {
            this.syncState.BlocksQueue.Dispose();
        }

        class WalletSyncManagerState
        {
            public readonly ISignals Signals;
            public readonly IBlockStore BlockStore;
            public readonly StoreSettings StoreSettings;

            public ChainedHeader WalletTip;

            /// <summary>Queue which contains blocks that should be processed by <see cref="WalletManager"/>.</summary>
            public IAsyncDelegateDequeuer<Block> BlocksQueue;

            /// <summary>Current <see cref="BlocksQueue"/> size in bytes.</summary>
            public long BlocksQueueSize;

            /// <summary>Flag to determine when the <see cref="MaxQueueSize"/> is reached.</summary>
            public bool MaxQueueSizeReached;

            public SubscriptionToken BlockConnectedSubscription;
            public SubscriptionToken TransactionReceivedSubscription;


            /// <summary>Limit <see cref="BlocksQueue"/> size to 100MB.</summary>
            public const int MaxQueueSize = 100 * 1024 * 1024;

            public WalletSyncManagerState(ISignals signals, IBlockStore blockStore, StoreSettings storeSettings)
            {
                this.Signals = signals;
                this.BlockStore = blockStore;
                this.StoreSettings = storeSettings;
            }
        }
    }
}
