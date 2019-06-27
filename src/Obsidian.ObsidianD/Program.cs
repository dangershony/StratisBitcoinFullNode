using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.Features.SegWitWallet;
using Obsidian.Features.SegWitWallet.Tests;
using Obsidian.ObsidianD.Api;
using Obsidian.ObsidianD.Temp;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

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
                CliTool.CliMain(argList.ToArray());
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
            PosBlockHeader.CustomPoWHash = ObsidianHash.GetObsidianXPoWHash;
            
            try
            {
                var nodeSettings = new NodeSettings(networksSelector: ObsidianNetworksSelector.Obsidian,
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, StratisNode", args: args)
                {
                    MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                };

                IFullNodeBuilder nodeBuilder = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings);

               

                IFullNode node = nodeBuilder.UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseSegWitWallet()
                    .AddPowPosMining()
                    .UseApiSlim()
                    .UseApps()
                    .AddRPC()
                    .Build();

              

#if DEBUG
                // test hook
                var fullNode = (FullNode)node;
                StaticWallet.CreateWallet(fullNode.Network, fullNode);

                _ = Task.Run(async () =>
                  {
                      await Task.Delay(15000);
                      TestBench.Run((FullNode)node);
                  });
#endif

              
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
            return $"oxd {Assembly.GetEntryAssembly()?.GetName().Version} (d)";
#else
			return $"oxd {Assembly.GetEntryAssembly()?.GetName().Version} (r)";
#endif
        }
    }
}
