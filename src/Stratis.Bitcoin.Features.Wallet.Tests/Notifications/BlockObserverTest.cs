using Moq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Notifications;
using Xunit;

namespace Stratis.Bitcoin.Features.Wallet.Tests.Notifications
{
    public class BlockObserverTest
    {
        [Fact]
        public void OnNextCoreProcessesOnTheWalletSyncManager()
        {
            var walletSyncManager = new Mock<IWalletSyncManager>();
            BlockObserver observer = new BlockObserver(walletSyncManager.Object);
            PowBlock powBlock = new PowBlock();

            observer.OnNext(powBlock);

            walletSyncManager.Verify(w => w.ProcessBlock(powBlock), Times.Exactly(1));
        }
    }
}
