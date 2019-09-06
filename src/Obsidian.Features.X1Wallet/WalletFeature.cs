using System.Collections.Generic;
using System.Text;
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
        readonly KeyAddressBalance[] keyAddressBalancesEmpty = new KeyAddressBalance[0];
        readonly WalletManagerWrapper walletManagerWrapper;
        readonly IConnectionManager connectionManager;
        readonly BroadcasterBehavior broadcasterBehavior;

        public WalletFeature(
            WalletManagerWrapper walletManagerWrapper,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            INodeStats nodeStats)
        {
            this.walletManagerWrapper = walletManagerWrapper;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;

            nodeStats.RegisterStats(AddComponentStats, StatsType.Component, this.GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, this.GetType().Name, 800);
        }

        public override Task InitializeAsync()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.walletManagerWrapper.Dispose();
        }

        void AddInlineStats(StringBuilder log)
        {
            string height = "n/a";
            string hash = "n/a";
            string walletName = null;

            using (var context = this.walletManagerWrapper.GetWalletContext(null, true))
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

            IEnumerable<KeyAddressBalance> balancesPerAddress = this.keyAddressBalancesEmpty;

            using (var context = this.walletManagerWrapper.GetWalletContext(null, true))
            {
                if (context != null)
                {
                    walletName = context.WalletManager.WalletName;
                    balancesPerAddress = context.WalletManager.GetBalances();
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

            Money confirmed = Money.Zero;
            Money unconfirmed = Money.Zero;
            Money spendable = Money.Zero;
            foreach (var bal in balancesPerAddress)
            {
                confirmed += bal.AmountConfirmed;
                unconfirmed += bal.AmountUnconfirmed;
                spendable += bal.SpendableAmount;
            }

            log.AppendLine(($"{walletName}").PadRight(LoggingConfiguration.ColumnLength + 10)
                           + (" Confirmed balance: " + confirmed.ToString()).PadRight(LoggingConfiguration.ColumnLength + 20)
                           + " Unconfirmed balance: " + unconfirmed.ToString().PadRight(LoggingConfiguration.ColumnLength + 20)
                           + " Spendable balance " + spendable.ToString()
                           );

        }
    }
}
