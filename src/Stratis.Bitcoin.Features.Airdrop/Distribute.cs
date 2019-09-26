using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Airdrop
{
    public class Distribute
    {
        private readonly Network network;
        private readonly INodeLifetime nodeLifetime;
        private readonly AirdropSettings airdropSettings;
        private readonly NodeSettings nodeSettings;
        private readonly IWalletManager walletManager;
        private readonly IWalletTransactionHandler walletTransactionHandler;
        private readonly ILogger logger;

        private UtxoContext utxoContext;

        public Distribute(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, AirdropSettings airdropSettings, NodeSettings nodeSettings, IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.airdropSettings = airdropSettings;
            this.nodeSettings = nodeSettings;
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.utxoContext = new UtxoContext(this.nodeSettings.DataDir, this.airdropSettings.SnapshotHeight.Value);
            this.utxoContext.Database.EnsureCreated();
        }

        public Task DistributeCoins(CancellationToken arg)
        {
            if (this.utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.Failed))
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            var inProgress = this.utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.InProgress);

            if (inProgress.Any())
            {
               // check if still in progress
               throw new NotImplementedException();
            }

            // This part must be manual, 
            var walletName = "";
            var accountName = "";
            var password = "";

            var accountReference = new WalletAccountReference(walletName, accountName);


            var progress = utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.InProgress ||
                d.Status == DistributeStatus.Failed);
            
            if (progress)
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            var outputs = this.utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.NoStarted).Take(10);

            var recipients = new List<Recipient>();
            foreach (UTXODistribute utxoDistribute in outputs)
            {
                // convert the script to the target address
                var address = utxoDistribute.Address;

                // Apply any ratio to the value.
                var amount = utxoDistribute.Value;

                recipients.Add(new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(address, this.network).ScriptPubKey,
                    Amount = amount
                });
            }

            HdAddress change = this.walletManager.GetUnusedChangeAddress(accountReference);

            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = accountReference,
                MinConfirmations = 0,
                WalletPassword = password,
                Recipients = recipients,
                ChangeAddress = change,
                UseSegwitChangeAddress = true,
                FeeType = FeeType.Low
            };

            Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

            // mark the database items as started

            // broadcast to the network

            // mark the database items as in progress

            // wait confirmations

            return Task.CompletedTask;
        }
    }
}