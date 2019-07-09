using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class X1WalletApiFeature : BaseWalletFeature
    {
        readonly WalletController walletController;

        public X1WalletApiFeature(
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
