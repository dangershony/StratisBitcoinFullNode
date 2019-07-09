using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.SegWitWallet.Web
{
    public class SegWitWalletApiFeature : BaseWalletFeature
    {
        readonly WalletController walletController;

        public SegWitWalletApiFeature(
            WalletController walletController,
            ILoggerFactory loggerFactory
          )
        {
            this.walletController = walletController;
        }


        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
           
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

       
    }
}
