using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;

namespace Obsidian.Features.SegWitWallet.Web
{
    public static class FullNodeFeature
    {
        public static IFullNodeBuilder UseSegWitWalletApi(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SegWitWalletApiFeature>(nameof(SegWitWalletApiFeature));

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SegWitWalletApiFeature>()
                    .DependOn<SegWitWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddTransient<SecureApiController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}