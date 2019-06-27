using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Policy;
using Obsidian.Features.SegWitWallet.Web;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Obsidian.Features.SegWitWallet
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is not a PoS network.</exception>
    public static class FullNodeFeature
    {
        public static IFullNodeBuilder UseSegWitWalletApi(this IFullNodeBuilder fullNodeBuilder)
        {
            LoggingConfiguration.RegisterFeatureNamespace<SegWitWalletApiFeature>("segwitwalletapi");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SegWitWalletApiFeature>()
                    .DependOn<SegWitWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddTransient<WalletWebApiController>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}