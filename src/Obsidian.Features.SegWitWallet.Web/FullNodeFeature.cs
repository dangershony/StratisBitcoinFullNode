using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public static class FullNodeFeature
    {
        public static IFullNodeBuilder UseX1WalletApi(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<X1WalletApiFeature>(nameof(X1WalletApiFeature));

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<X1WalletApiFeature>()
                    .DependOn<WalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddTransient<SecureApiController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}