﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using DBreeze.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BitcoinCore;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
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
        private readonly Network network;
        private readonly INodeLifetime nodeLifetime;
        private readonly ISignals signals;
        private readonly ChainIndexer chainIndexer;
        private readonly AirdropSettings airdropSettings;
        private readonly NodeSettings nodeSettings;
        private readonly DBreezeSerializer dBreezeSerializer;
        private readonly IAsyncProvider asyncProvider;
        private readonly CachedCoinView cachedCoinView;
        private SubscriptionToken blockConnectedSubscription;
        private readonly ILogger logger;

        public AirdropFeature(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, ISignals signals, ChainIndexer chainIndexer, AirdropSettings airdropSettings, NodeSettings nodeSettings, ICoinView cachedCoinView, DBreezeSerializer dBreezeSerializer, IAsyncProvider asyncProvider)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.signals = signals;
            this.chainIndexer = chainIndexer;
            this.airdropSettings = airdropSettings;
            this.nodeSettings = nodeSettings;
            this.dBreezeSerializer = dBreezeSerializer;
            this.asyncProvider = asyncProvider;
            this.cachedCoinView = (CachedCoinView)cachedCoinView;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

        }

        public override Task InitializeAsync()
        {
            if (this.airdropSettings.SnapshotMode && this.airdropSettings.SnapshotHeight > 0)
            {
                this.blockConnectedSubscription = this.signals.Subscribe<BlockConnected>(this.OnBlockConnected);
            }

            if (this.airdropSettings.DistributeMode)
            {
                this.asyncProvider.CreateAndRunAsyncLoop("airdrop-distribute", this.Distribute, nodeLifetime.ApplicationStopping);
            }

            return Task.CompletedTask;
        }

        private Task Distribute(CancellationToken arg)
        {
            throw new NotImplementedException();
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

                UtxoContext utxoContext = new UtxoContext(this.nodeSettings.DataDir);

                utxoContext.Database.EnsureCreated();

                Money total = 0;
                int count = 0;
                foreach (var item in this.IterateUtxoSet(dBreezeCoinView))
                {
                    if (item.TxOut.IsEmpty) 
                        continue;

                    if (count % 100 == 0 && this.nodeLifetime.ApplicationStopping.IsCancellationRequested)
                        return;

                    total += item.TxOut.Value;
                    count++;

                    var addressItem = GetAddress(this.network, item.TxOut.ScriptPubKey);

                    utxoContext.UnspentOutputs.Add(new UTXO()
                    {
                        Trxid = item.OutPoint.ToString(),
                        Script = item.TxOut.ScriptPubKey.ToString(),
                        Value = item.TxOut.Value,
                        Address = addressItem.address,
                        ScriptType = addressItem.scriptType.ToString(),
                        Height = item.Height
                    });

                    if (count % 10000 == 0)
                    {
                        utxoContext.SaveChanges();
                    }

                    this.logger.LogInformation("OutPoint = {0} - TxOut = {1} total = {2} count = {3}", item.OutPoint, item.TxOut, total, count);
                }

                utxoContext.SaveChanges();

                this.logger.LogInformation("Finished snapshot");
            }
        }

        public static (TxOutType scriptType, string address) GetAddress(Network network, Script script)
        {
            var template = NBitcoin.StandardScripts.GetTemplateFromScriptPubKey(script);

            if (template == null)
                return (TxOutType.TX_NONSTANDARD, string.Empty);

            if (template.Type == TxOutType.TX_NONSTANDARD)
                return (TxOutType.TX_NONSTANDARD, string.Empty);

            if (template.Type == TxOutType.TX_NULL_DATA)
                return (template.Type, string.Empty);

            if (template.Type == TxOutType.TX_PUBKEY)
            {
                var pubkeys = script.GetDestinationPublicKeys(network);
                return (template.Type, pubkeys[0].GetAddress(network).ToString());
            }

            if (template.Type == TxOutType.TX_PUBKEYHASH ||
                template.Type == TxOutType.TX_SCRIPTHASH ||
                template.Type == TxOutType.TX_SEGWIT)
            {
                BitcoinAddress bitcoinAddress = script.GetDestinationAddress(network);
                if (bitcoinAddress != null)
                {
                    return (template.Type, bitcoinAddress.ToString());
                }
            }

            if (template.Type == TxOutType.TX_MULTISIG)
            {
                // TODO;
                return (template.Type, string.Empty);
            }

            if (template.Type == TxOutType.TX_COLDSTAKE)
            {
                //if (ColdStakingScriptTemplate.Instance.ExtractScriptPubKeyParameters(script, out KeyId hotPubKeyHash, out KeyId coldPubKeyHash))
                //{
                //    // We want to index based on both the cold and hot key
                //    return new[]
                //    {
                //        hotPubKeyHash.GetAddress(network).ToString(),
                //        coldPubKeyHash.GetAddress(network).ToString(),
                //    };
                //}

                return (template.Type, string.Empty);
            }

            // Fail the node in such cases (all script types must be covered)
            throw new Exception("Unknown script type");
        }

        public IEnumerable<(OutPoint OutPoint, TxOut TxOut, int Height)> IterateUtxoSet(DBreezeCoinView dBreezeCoinView)
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
                            yield return (new OutPoint(trxHash, i), coins.Outputs[i], (int)coins.Height);
                        }
                    }
                }
            }
        }

        public class UtxoContext : DbContext
        {
            private readonly string path;
            public DbSet<UTXO> UnspentOutputs { get; set; }

            public UtxoContext(string path)
            {
                this.path = path;
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlite($@"Data Source={this.path}\snapshot.db");
            }
        }

        public class UTXO
        {
            [Key]
            public string Trxid { get; set; }
            public string Script { get; set; }
            public string Address { get; set; }
            public string ScriptType { get; set; }
            public long Value { get; set; }
            public int Height { get; set; }
        }
    }
}
