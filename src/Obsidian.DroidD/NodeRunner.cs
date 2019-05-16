using System;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.DroidD.Node;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.DroidD
{
    public class NodeRunner
    {
        public void Run()
        {

        }

        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
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
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, StratisNode", args: args)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                IFullNodeBuilder builder = new FullNodeBuilder();


                builder = builder.UseNodeSettings(nodeSettings);
                builder = builder.UseBlockStore();
                builder = builder.UsePosConsensus();
                builder = builder.UseMempool();
                builder = builder.UseColdStakingWallet();
                builder = builder.AddPowPosMining();
                //.UseApi()
                builder = builder.UseApps();
                builder = builder.AddRPC();
                IFullNode node = builder.Build();

                if (node != null)
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
            return $"Obsidian.DroidD {Assembly.GetEntryAssembly()?.GetName().Version} (Debug)";
#else
			return $"ObsidianD {Assembly.GetEntryAssembly()?.GetName().Version} (Release)";
#endif
        }
    }
}