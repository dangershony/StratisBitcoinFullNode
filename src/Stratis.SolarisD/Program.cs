using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.SolarisD
{
    public class Program
    {
        private const string DataDirRootArgument = "datadirroot";
        private const string Agent = "SolarisNode";
        private const string DataDirRoot = "SolarisNode";

        public static async Task Main(string[] args)
        {
            try
            {
                if (!ContainsDataDirRoot(args))
                    args = AddDefaultDataDirRoot(args);

                var nodeSettings = new NodeSettings(
                    networksSelector: Networks.Solaris, 
                    protocolVersion: ProtocolVersion.PROTOCOL_VERSION,
                    agent: Agent,
                    args: args)
                {
                    MinProtocolVersion = ProtocolVersion.PROTOCOL_VERSION
                };

                IFullNode node = new FullNodeBuilder()
                    .UseNodeSettings(nodeSettings)
                    .UseBlockStore()
                    .UsePosConsensus()
                    .UseMempool()
                    .UseColdStakingWallet()
                    .AddPowPosMining()
                    .UseApi()
                    .UseApps()
                    .AddRPC()
                    .Build();

                if (node != null)
                    await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a problem initializing the node. Details: '{ex}'");
            }
        }

        
        public static bool ContainsDataDirRoot(string[] arguments)
        {
            return
                (
                    from argument in arguments
                    let split = argument.Split('=')
                    select split[0]
                )
                .Any(
                    argument =>
                        argument.Equals($"-{DataDirRootArgument}") ||
                        argument.Equals(DataDirRootArgument)
                );
        }

        public static string[] AddDefaultDataDirRoot(string[] arguments)
        {
            return arguments.Concat(new[] {$"-{DataDirRootArgument}={DataDirRoot}"}).ToArray();
        }
    }
}
