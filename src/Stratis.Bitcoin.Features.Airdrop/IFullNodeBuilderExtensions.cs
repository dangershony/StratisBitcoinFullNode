using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Configuration.Settings;
using Stratis.Bitcoin.Features.Airdrop;
using Stratis.Bitcoin.Features.Consensus;

namespace Stratis.Bitcoin.Features.Dns
{
    /// <summary>
    /// Extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class IFullNodeBuilderExtensions
    {
        /// <summary>
        /// Configures the Dns feature.
        /// </summary>
        /// <param name="fullNodeBuilder">Full node builder used to configure the feature.</param>
        /// <returns>The full node builder with the Dns feature configured.</returns>
        public static IFullNodeBuilder TakeSnapshot(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<AirdropFeature>("airdrop");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                .AddFeature<AirdropFeature>()
                .DependOn<ConsensusFeature>()
                .FeatureServices(services =>
                {
                    services.AddSingleton(fullNodeBuilder);
                    services.AddSingleton<AirdropSettings>();
                });
            });

            return fullNodeBuilder;
        }
    }
}