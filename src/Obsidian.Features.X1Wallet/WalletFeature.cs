﻿using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet
{
    /// <inheritdoc />
    public class WalletFeature : BaseWalletFeature
    {
        readonly WalletManagerFactory walletManagerFactory;
        readonly IConnectionManager connectionManager;
        readonly BroadcasterBehavior broadcasterBehavior;
        readonly Network network;

        public WalletFeature(
            WalletManagerFactory walletManagerFactory,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            INodeStats nodeStats, Network network)
        {
            this.walletManagerFactory = walletManagerFactory;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;
            this.network = network;

            nodeStats.RegisterStats(AddComponentStats, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, GetType().Name, 800);
        }

        public override Task InitializeAsync()
        {
            HashStringExtensions.Init(this.network);

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.walletManagerFactory.Dispose();
        }

        void AddInlineStats(StringBuilder log)
        {
            string height = "n/a";
            string hash = "n/a";
            string walletName = null;

            using (var context = this.walletManagerFactory.GetWalletContext(null, true))
            {
                if (context != null)
                {
                    height = context.WalletManager.WalletLastBlockSyncedHeight.ToString();
                    hash = context.WalletManager.WalletLastBlockSyncedHash?.ToString() ?? "n/a";
                    walletName = context.WalletManager.WalletName;
                }
            }
            if (walletName != null)
            {
                log.AppendLine($"Wallet {walletName}: Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + height.PadRight(8) +
                               (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hash));
            }
            else
            {
                log.AppendLine("No wallet loaded.");
            }
        }

        void AddComponentStats(StringBuilder log)
        {
            string walletName = null;
            var balance = new Balance { AmountConfirmed = Money.Zero, AmountUnconfirmed = Money.Zero, SpendableAmount = Money.Zero };

            using (var context = this.walletManagerFactory.GetWalletContext(null, true))
            {
                if (context != null)
                {
                    walletName = context.WalletManager.WalletName;
                    context.WalletManager.GetBudget(out balance);
                }
            }

            if (walletName == null)
            {
                log.AppendLine();
                log.AppendLine("======X1 Wallet======");
                log.AppendLine("No wallet file loaded.");
                return;
            }

            log.AppendLine();
            log.AppendLine("======X1 Wallet======");

            

            log.AppendLine(($"{walletName}").PadRight(LoggingConfiguration.ColumnLength + 10)
                           + (" Confirmed balance: " + balance.AmountConfirmed.ToString()).PadRight(LoggingConfiguration.ColumnLength + 20)
                           + " Unconfirmed balance: " + balance.AmountUnconfirmed.ToString().PadRight(LoggingConfiguration.ColumnLength + 20)
                           + " Spendable balance " + balance.SpendableAmount.ToString()
                           );

        }
    }
}
