using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Policy;
using Obsidian.Features.X1Wallet.Adapters;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;

namespace Obsidian.Features.X1Wallet
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

            LoggingConfiguration.RegisterFeatureNamespace<WalletFeature>(nameof(WalletFeature));

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<WalletFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<BlockStoreFeature>()
                    //.DependOn<RPCFeature>()
                    .FeatureServices(services =>
                    {
                        // WalletManagerWrapper implements 2 interfaces, ensure the same instance is used when resolved.
                        services.AddSingleton<WalletManagerWrapper>();
                        //services.AddSingleton<IWalletManager>(x => x.GetRequiredService<WalletManagerWrapper>());
                        //services.AddSingleton<IWalletSyncManager>(x => x.GetRequiredService<WalletManagerWrapper>());
                        services.AddSingleton<IWalletManager, IWalletManagerStakingAdapter>();
                        services.AddSingleton<IPowMining, PowMining>();
                        services.AddSingleton<IPosMinting, Staking>();
                        services.AddSingleton<BlockDefinition, PowBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosBlockDefinition>();
                        services.AddSingleton<BlockDefinition, PosPowBlockDefinition>();
                        services.AddSingleton<IBlockProvider, BlockProvider>();
                        services.AddSingleton<MinerSettings>();
                        services.AddSingleton<IWalletTransactionHandler, TransactionHandler>();
                        services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                        services.AddSingleton<WalletSettings>();
                        services.AddTransient<WalletController>();
                        //services.AddSingleton<WalletRPCController>();
                        services.AddSingleton<IBroadcasterManager, FullNodeBroadcasterManager>();
                        services.AddSingleton<BroadcasterBehavior>();
                        services.AddSingleton<IScriptAddressReader>(new ScriptAddressReader());
                        services.AddSingleton<StandardTransactionPolicy>();
                        services.AddSingleton<IAddressBookManager, AddressBookManager>();
                    });
            });

            return fullNodeBuilder;
        }
    }
}