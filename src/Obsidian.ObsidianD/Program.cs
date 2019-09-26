using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.SecureApi;
using Obsidian.Networks.Obsidian;
using Obsidian.ObsidianD.Api;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Features.Airdrop;

namespace Obsidian.ObsidianD
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0 && args[0] == "cli")
            {
                var argList = args.ToList();
                argList.RemoveAt(0);
                Cli.CliMain(argList.ToArray());
            }
            else
            {
                MainAsync(args).Wait();
            }
        }

        /// <summary> Starts the fullnode asyncronously.</summary>
        /// <remarks>To run as gateway node use the args -gateway=1 -whitelist=[trusted-QT-ip] addnode=[trusted-QT-ip],
        /// e.g. -gateway=1 -whitelist=104.45.21.229 addnode=104.45.21.229 -port=56666
        /// Use arg -maxblkmem=2 on a low memory VPS.
        /// </remarks>
        /// <param name="args">args</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        static async Task MainAsync(string[] args)
        {
            PosBlockHeader.CustomPoWHash = ObsidianHash.GetObsidianPoWHash;

            try
            {
                var nodeSettings = new NodeSettings(networksSelector: ObsidianNetworksSelector.Obsidian,
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, Stratis ", args: args)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };


                var useHDWallet = false;

                var builder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .Airdrop()
                    .UseMempool();


                if (useHDWallet)
                {
                    builder
                        .AddPowPosMining()
                        .AddRPC()
                        .UseColdStakingWallet()
                        .UseApi();
                }
                else
                {
                    builder.UseX1Wallet()
                        .UseX1WalletApi()
                        .UseSecureApiHost();
                }

                var node = builder.Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine(@"There was a problem initializing the node. Details: '{0}'", ex.Message);
            }
        }

        static string GetName()
        {
#if DEBUG
            return $"ObsidianD {Assembly.GetEntryAssembly()?.GetName().Version} (D)";
#else
			return $"ObsidianD {Assembly.GetEntryAssembly()?.GetName().Version} (R)";
#endif
        }
    }
}
