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
using Stratis.Bitcoin.Interfaces;
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
        private readonly IBroadcasterManager broadcasterManager;
        private readonly IBlockStore blockStore;
        private readonly ILogger logger;

        private UtxoContext utxoContext;

        public Distribute(Network network, INodeLifetime nodeLifetime, ILoggerFactory loggerFactory, AirdropSettings airdropSettings, NodeSettings nodeSettings, IWalletManager walletManager, IWalletTransactionHandler walletTransactionHandler, IBroadcasterManager broadcasterManager, IBlockStore blockStore)
        {
            this.network = network;
            this.nodeLifetime = nodeLifetime;
            this.airdropSettings = airdropSettings;
            this.nodeSettings = nodeSettings;
            this.walletManager = walletManager;
            this.walletTransactionHandler = walletTransactionHandler;
            this.broadcasterManager = broadcasterManager;
            this.blockStore = blockStore;

            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        public void Initialize()
        {
            this.utxoContext = new UtxoContext(this.nodeSettings.DataDir, this.airdropSettings.SnapshotHeight.Value);
            this.utxoContext.Database.EnsureCreated();
        }

        public Task DistributeCoins(CancellationToken arg)
        {
            // Check for invalid db states
            if (this.utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.Failed))
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            // Check for distributed trx still in progress (unconfirmed yet)
            var inProgress = this.utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.InProgress);

            if (inProgress.Any())
            {
                bool foundTrxNotInBlock = false;
                foreach (var utxoDistribute in inProgress)
                {
                    var trx = this.blockStore?.GetTransactionById(new uint256(utxoDistribute.Trxid));

                    if (trx == null)
                    {
                        foundTrxNotInBlock = true;
                    }
                    else
                    {
                        utxoDistribute.Status = DistributeStatus.Complete;
                    }
                }

                this.utxoContext.SaveChanges(true);

                if(foundTrxNotInBlock)
                    return Task.CompletedTask;
            }

            // This part must be manual, 
            var walletName = "";
            var accountName = "";
            var password = "";

            var accountReference = new WalletAccountReference(walletName, accountName);

            var progress = this.utxoContext.DistributeOutputs.Any(d =>
                d.Status == DistributeStatus.Started ||
                d.Status == DistributeStatus.InProgress ||
                d.Status == DistributeStatus.Failed);
            
            if (progress)
            {
                this.logger.LogError("database is in an invalid state");
                return Task.CompletedTask;
            }

            var outputs = this.utxoContext.DistributeOutputs.Where(d => d.Status == DistributeStatus.NoStarted).Take(10);

            // mark the database items as started
            foreach (UTXODistribute utxoDistribute in outputs)
                utxoDistribute.Status = DistributeStatus.Started;
            this.utxoContext.SaveChanges(true);

            try
            {
                var recipients = new List<Recipient>();
                foreach (UTXODistribute utxoDistribute in outputs)
                {
                    try
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
                    catch (Exception e)
                    {
                        utxoDistribute.Error = e.Message;
                        utxoDistribute.Status = DistributeStatus.Failed;
                        this.utxoContext.SaveChanges(true);
                        return Task.CompletedTask;
                    }
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

                // broadcast to the network
                this.broadcasterManager.BroadcastTransactionAsync(transactionResult);

                // mark the database items as in progress
                foreach (UTXODistribute utxoDistribute in outputs)
                    utxoDistribute.Status = DistributeStatus.InProgress;
                this.utxoContext.SaveChanges(true);
            }
            catch (Exception e)
            {
                foreach (UTXODistribute utxoDistribute in outputs)
                {
                    utxoDistribute.Error = e.Message;
                    utxoDistribute.Status = DistributeStatus.Failed;
                }
                this.utxoContext.SaveChanges(true);
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }
    }
}