using System;
using System.Reflection;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.SecureApi;
using Obsidian.Networks.ObsidianX;
using Obsidian.x1d.Api;
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
            MainAsync(args).Wait();
        }

        static async Task MainAsync(string[] args)
        {
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
                      await Task.Delay(30000);
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
            // ReSharper disable once UnreachableCode
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
