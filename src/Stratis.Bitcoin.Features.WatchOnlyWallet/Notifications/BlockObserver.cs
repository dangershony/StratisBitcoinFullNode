using NBitcoin;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.WatchOnlyWallet.Notifications
{
    /// <summary>
    /// Observer that receives notifications about the arrival of new <see cref="PowBlock"/>s.
    /// </summary>
    public class BlockObserver : SignalObserver<PowBlock>
    {
        private readonly IWatchOnlyWalletManager walletManager;

        public BlockObserver(IWatchOnlyWalletManager walletManager)
        {
            this.walletManager = walletManager;
        }

        /// <summary>
        /// Manages what happens when a new block is received.
        /// </summary>
        /// <param name="powBlock">The new block.</param>
        protected override void OnNextCore(PowBlock powBlock)
        {
            this.walletManager.ProcessBlock(powBlock);
        }
    }
}
