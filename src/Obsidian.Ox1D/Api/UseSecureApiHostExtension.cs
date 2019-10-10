using Microsoft.Extensions.DependencyInjection;
using Obsidian.Features.X1Wallet.SecureApi;
using Stratis.Bitcoin.Builder;

namespace Obsidian.OxD.Api
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    public static class UseSecureApiHostExtension
    {
        public static IFullNodeBuilder UseSecureApiHost(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SecureApiHostFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton(fullNodeBuilder);
                        services.AddSingleton<SecureApiSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}