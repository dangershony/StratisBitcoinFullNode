using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.Features.X1Wallet;
using Obsidian.Features.X1Wallet.SecureApi;
using Obsidian.Networks.ObsidianX;
using Obsidian.x1d.Api;
using Obsidian.x1d.Cli;
using Obsidian.x1d.Temp;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.x1d
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

       
        static async Task MainAsync(string[] args)
        {
            PosBlockHeader.CustomPoWHash = ObsidianXHash.GetObsidianXPoWHash;

            try
            {
                var nodeSettings = new NodeSettings(networksSelector: ObsidianXNetworksSelector.Obsidian,
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}", args: args)
                {
                    MinProtocolVersion = ProtocolVersion.PROVEN_HEADER_VERSION
                };

                var builder = new FullNodeBuilder()
                            .UseNodeSettings(nodeSettings)
                            .UseBlockStore()
                            .UsePosConsensus()
                            .UseMempool()
                            .UseX1Wallet()
                            .UseX1WalletApi()
                            .UseSecureApiHost();

                var node = builder.Build();

//#if DEBUG
                _ = Task.Run(async () =>
                  {
                      await Task.Delay(7500);
                      TestBench.RunTestCodeAsync((FullNode)node);  // start mining to the wallet
                  });
//#endif

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occured in {GetName()}: {ex.Message}");
            }
        }

        static string GetName()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            var name = assembly.Name;
            var version = assembly.Version;
            var compilation = IsDebug ? " (Debug)" : "";
            return $"{name} {version}{compilation}";

        }

#if DEBUG
        const bool IsDebug = true;
#else
		public const bool IsDebug = false;
#endif

    }
}
