using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.LightWallet;
using Stratis.Bitcoin.Features.Notifications;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.SnowD
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                bool isStratis = args.Contains("stratis");
                bool isSolaris = args.Contains("solaris");

                NodeSettings nodeSettings;

                IFullNodeBuilder fullNodeBuilder = null;

                if (!args.Any(a => a.Contains("datadirroot")))
                    args = args.Concat(new[] { "-datadirroot=SolarisSnow" }).ToArray();

                if (isStratis)
                {
                    nodeSettings = new NodeSettings(networksSelector: Networks.Stratis, protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: "Breeze", args: args)
                    {
                        MinProtocolVersion = ProtocolVersion.ALT_PROTOCOL_VERSION
                    };

                    fullNodeBuilder = new FullNodeBuilder()
                                    .UseNodeSettings(nodeSettings)
                                    .UseApi()
                                    .UseBlockStore()
                                    .UsePosConsensus()
                                    .UseLightWallet()
                                    .UseBlockNotification()
                                    .UseTransactionNotification();
                }
                else if (isSolaris)
                {
                    nodeSettings = new NodeSettings(networksSelector: Networks.Solaris, protocolVersion: ProtocolVersion.PROTOCOL_VERSION, agent: "Snow", args: args)
                    {
                        MinProtocolVersion = ProtocolVersion.PROTOCOL_VERSION
                    };

                    fullNodeBuilder = new FullNodeBuilder()
                        .UseNodeSettings(nodeSettings)
                        .UseApi()
                        .UseBlockStore()
                        .UsePosConsensus()
                        .UseLightWallet()
                        .UseBlockNotification()
                        .UseTransactionNotification();
                }
                else
                {
                    nodeSettings = new NodeSettings(networksSelector: Networks.Bitcoin, agent: "Breeze", args: args);

                    fullNodeBuilder = new FullNodeBuilder()
                                    .UseNodeSettings(nodeSettings)
                                    .UseApi()
                                    .UseBlockStore()
                                    .UsePowConsensus()
                                    .UseLightWallet()
                                    .UseBlockNotification()
                                    .UseTransactionNotification();
                }

                IFullNode node = fullNodeBuilder.Build();

                await node.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"There was a problem initializing the node: '{ex}'");
            }
        }
    }
}
