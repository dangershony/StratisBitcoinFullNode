using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Signals;

namespace Stratis.Bitcoin.Features.Airdrop
{
    public class AirdropFeature : FullNodeFeature
    {
        private readonly Network network;
        private readonly ISignals signals;
        private readonly ChainIndexer chainIndexer;
        private readonly AirdropSettings airdropSettings;
        public string FilenamePrefix { get; set; }

        private SubscriptionToken blockConnectedSubscription;

        public AirdropFeature(Network network, ISignals signals, ChainIndexer chainIndexer, AirdropSettings airdropSettings)
        {
            this.network = network;
            this.signals = signals;
            this.chainIndexer = chainIndexer;
            this.airdropSettings = airdropSettings;

            if (airdropSettings.SnapshotHeight != null)
            {
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            }
        }

        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        private void OnBlockConnected(BlockConnected blockConnected)
        {
            if (blockConnected.ConnectedBlock.ChainedHeader.Height == this.airdropSettings.SnapshotHeight)
            {
                // Take a snapshot of the chain.
            }
        }
    }
}
