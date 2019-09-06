using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.Protocol;
using Obsidian.DroidD.Logging;
using Obsidian.Networks.Obsidian;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.DroidD
{
    public sealed class NodeController
    {
        public event EventHandler FullNodeStopRequested;
        public event EventHandler NodeCrashed;
        public ConcurrentQueue<XamarinLogger.XamarinLoggerEventArgs> LogQueue;

        FullNode _fullNode;

        public void StartFullNode(string[] startParameters)
        {
            PosBlockHeader.CustomPoWHash = ObsidianHash.GetObsidianPoWHash;

            try
            {
                var nodeSettings = new NodeSettings(networksSelector: ObsidianNetworksSelector.Obsidian,
                    protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, StratisNode", args: startParameters)
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
                builder = builder.AddRPC();
                _fullNode = (FullNode)builder.Build();

                LogQueue = XamarinLogger.Queue;

                if (_fullNode != null)
                    Task.Run(async () => await RunAsync(_fullNode));

            }
            catch (Exception ex)
            {
                Console.WriteLine(@"There was a problem initializing or running the node. Details: '{0}'", ex.Message);
                NodeCrashed?.Invoke(this, EventArgs.Empty);
            }
        }

        public (int height, bool synced) GetBlockStoreInfo()
        {
            if (_fullNode != null && _fullNode.ChainBehaviorState != null && _fullNode.ChainBehaviorState.BlockStoreTip != null)
                return (_fullNode.ChainBehaviorState.BlockStoreTip.Height, _fullNode.ChainBehaviorState.IsAtBestChainTip);
            return (-1, false);
        }


        public void RaiseNodeShutdownRequested()
        {
            FullNodeStopRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Installs handlers for graceful shutdown in the console, starts a full node and waits until it terminates.
        /// </summary>
        /// <param name="node">Full node to run.</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        async Task RunAsync(IFullNode node)
        {
            var done = new ManualResetEventSlim(false);
            using (var cts = new CancellationTokenSource())
            {
                Action shutdown = () =>
                {
                    if (!cts.IsCancellationRequested)
                    {
                        Console.WriteLine(@"Application is shutting down.");
                        try
                        {
                            cts.Cancel();
                        }
                        catch (ObjectDisposedException exception)
                        {
                            Console.WriteLine(exception.Message);
                        }
                    }

                    done.Wait();
                };

                FullNodeStopRequested += (sender, eventArgs) =>
                {
                    shutdown();
                };

                try
                {
                    await node.RunAsync(cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    done.Set();
                    _fullNode = null;
                }
            }
        }

        public string GetLog()
        {
            var sb = new StringBuilder();
            while (LogQueue != null && LogQueue.TryDequeue(out var item))
            {
                sb.AppendLine(item.Text);
            }
            return sb.ToString();
        }

        static string GetName()
        {
#if DEBUG
            return $"Obsidian.DroidD {Assembly.GetEntryAssembly()?.GetName().Version} (Debug)";
#else
			return $"Obsidian.DroidD {Assembly.GetEntryAssembly()?.GetName().Version} (Release)";
#endif
        }
    }
}