using Microsoft.Extensions.DependencyInjection;
using Stratis.Bitcoin.Builder;

namespace Obsidian.ObsidianD.Api
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class FullNodeFeature
    {
        public static IFullNodeBuilder UseX1WalletApiHost(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<ApiFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton<ApiSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}