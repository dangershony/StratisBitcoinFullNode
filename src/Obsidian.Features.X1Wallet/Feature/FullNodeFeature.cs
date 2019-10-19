using System;
using Microsoft.Extensions.DependencyInjection;
using Obsidian.Features.X1Wallet.Staking;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Mining;

namespace Obsidian.Features.X1Wallet.Feature
{
    /// <summary>
    /// A class providing extension methods for <see cref="IFullNodeBuilder"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if this is not a PoS network.</exception>
    public static class FullNodeFeature
    {
        public static IFullNodeBuilder UseX1Wallet(this IFullNodeBuilder fullNodeBuilder)
        {
            if (!fullNodeBuilder.Network.Consensus.IsProofOfStake)
                throw new InvalidOperationException("A Proof-of-Stake network is required.");

            // Register the cold staking script template.
            fullNodeBuilder.Network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);

            LoggingConfiguration.RegisterFeatureNamespace<X1WalletFeature>(nameof(X1WalletFeature));

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<X1WalletFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<BlockStoreFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<WalletManagerFactory>();
                        services.AddSingleton<StakingCore>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosPowBlockDefinition>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<WalletSettings>();
                        services.AddTransient<WalletController>();
                        services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                        services.AddSingleton<BroadcasterBehavior>();
                        services.AddSingleton<MinerSettings>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}