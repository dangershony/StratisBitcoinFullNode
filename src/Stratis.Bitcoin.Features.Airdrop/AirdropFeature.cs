using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.EventBus;
using Stratis.Bitcoin.EventBus.CoreEvents;
using Stratis.Bitcoin.Features.Consensus.CoinViews;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Airdrop
{
    /// <summary>
    /// A feature that will take a snapshot of the UTXO set and create a json file with the results.
    /// </summary>
    public class AirdropFeature : FullNodeFeature
    {
        public const string FilenamePrefix = "snapshot-{height}-{count}.json";

        private readonly Network network;
        private readonly INodeLifetime nodeLifetime;
        private readonly ISignals signals;
        private readonly ChainIndexer chainIndexer;
        private readonly AirdropSettings airdropSettings;
        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly CachedCoinView cachedCoinView;
        private SubscriptionToken blockConnectedSubscription;
        private readonly ILogger logger;

        public AirdropFeature(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, ISignals signals, ChainIndexer chainIndexer, AirdropSettings airdropSettings, ICoinView cachedCoinView, DBreezeSerializer dBreezeSerializer)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.signals = signals;
            this.chainIndexer = chainIndexer;
            this.airdropSettings = airdropSettings;
            this.dBreezeSerializer = dBreezeSerializer;
            this.cachedCoinView = (CachedCoinView)cachedCoinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

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

                // From here consensus will stop advancing until the snapshot is done.
                this.logger.LogInformation("Starting snapshot at height {0}", blockConnected.ConnectedBlock.ChainedHeader.Height);

                // Ensure nothing is in cache.
                this.cachedCoinView.Flush(true);

                DBreezeCoinView dBreezeCoinView = (DBreezeCoinView)this.cachedCoinView.Inner; // ugly hack.

                Money total = 0;
                int count = 0;
                foreach (var item in this.IterateUtxoSet(dBreezeCoinView))
                {
                    if (item.TxOut.Value == 0) 
                        continue;

                    if (count % 100 == 0 && this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    total += item.TxOut.Value;
                    count++;

                    this.logger.LogInformation("OutPoint = {0} - TxOut = {1} total = {2} count = {3}", item.OutPoint, item.TxOut, total, count);
                }

                this.logger.LogInformation("Finished snapshot");
            }
        }

        public IEnumerable<(OutPoint OutPoint, TxOut TxOut)> IterateUtxoSet(DBreezeCoinView dBreezeCoinView)
        {
            using (DBreeze.Transactions.Transaction transaction = dBreezeCoinView.CreateTransaction())
            {
                transaction.SynchronizeTables("Coins");
                transaction.ValuesLazyLoadingIsOn = false;

                IEnumerable<Row<byte[], byte[]>> rows = transaction.SelectForward<byte[], byte[]>("Coins");

                foreach (Row<byte[], byte[]> row in rows)
                {
                    Coins coins = this.dBreezeSerializer.Deserialize<Coins>(row.Value);
                    uint256 trxHash = new uint256(row.Key);

                    for (int i = 0; i < coins.Outputs.Count; i++)
                    {
                        if (coins.Outputs[i] != null)
                        {
                            this.logger.LogDebug("UTXO for '{0}' position {1}.", trxHash, i);
                            yield return (new OutPoint(trxHash, i), coins.Outputs[i]);
                        }
                    }
                }
            }
        }
    }
}
