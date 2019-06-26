using System;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Policy;
using Obsidian.Features.SegWitWallet.Controllers;
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
    public static class FullNodeBuilderSegWitWalletExtension
    {
        public static IFullNodeBuilder UseSegWitWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            if (!fullNodeBuilder.Network.Consensus.IsProofOfStake)
                throw new InvalidOperationException("Cold staking can only be used on a PoS network.");

            // Register the cold staking script template.
            fullNodeBuilder.Network.StandardScriptsRegistry.RegisterStandardScriptTemplate(ColdStakingScriptTemplate.Instance);

            LoggingConfiguration.RegisterFeatureNamespace<SegWitWalletFeature>("segwitwallet");

            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<SegWitWalletFeature>()
                    .DependOn<MempoolFeature>()
                    .DependOn<BlockStoreFeature>()
                    .DependOn<RPCFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, WalletSyncManager>();
                        services.AddSingleton<IWalletTransactionHandler, SegWitWalletTransactionHandler>();
                        services.AddSingleton<SegWitWalletManager>();
                        services.AddSingleton<IWalletManager, WalletManagerFacade>();
                        services.AddSingleton<IWalletFeePolicy, WalletFeePolicy>();
                        services.AddSingleton<WalletSettings>();
                        //services.AddSingleton<ColdStakingController>();
                        services.AddTransient<SegWitWalletController>();
                        services.AddSingleton<WalletRPCController>();
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