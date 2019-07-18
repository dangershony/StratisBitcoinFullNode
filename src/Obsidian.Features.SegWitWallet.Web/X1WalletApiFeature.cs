using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class X1WalletApiFeature : Stratis.Bitcoin.Builder.Feature.FullNodeFeature
    {
        readonly WalletController walletController;

        public X1WalletApiFeature(WalletController walletController, ILoggerFactory loggerFactory
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

        /// <summary>
        /// Prints the help information on how to configure the settings to the logger.
        /// </summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            SecureApiSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            SecureApiSettings.BuildDefaultConfigurationFile(builder, network);
        }
    }
}
