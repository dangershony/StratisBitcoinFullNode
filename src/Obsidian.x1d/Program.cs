using System;
using System.Threading.Tasks;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.SecureApi;
using Obsidian.x1d.Api;
using Obsidian.x1d.Util;
using Stratis.Bitcoin.Builder;
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
                var nodeSettings = Init.GetNodeSettings(args);

                var builder = new FullNodeBuilder()
                            .UseNodeSettings(nodeSettings)
                            .UseBlockStore()
                            .UsePosConsensus()
                            .UseMempool()
                            .UseX1Wallet()
                            .UseX1WalletApi()
                            .UseSecureApiHost();

                var node = builder.Build();

                Init.PrintWelcomeMessage(nodeSettings, node);

                Init.RunIfDebugModeDelayed(node);

                await node.RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occured in {Init.GetName()}: {e.Message}");
            }
        }
    }
}
