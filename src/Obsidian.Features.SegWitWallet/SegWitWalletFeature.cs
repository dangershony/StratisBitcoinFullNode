using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.ColdStaking.Controllers;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Controllers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.SegWitWallet
{
    /// <inheritdoc />
    public class SegWitWalletFeature : BaseWalletFeature
    {
        readonly IWalletSyncManager walletSyncManager;
        readonly SegWitWalletManager walletManager;
        readonly IConnectionManager connectionManager;
        readonly IAddressBookManager addressBookManager;
        readonly BroadcasterBehavior broadcasterBehavior;

        public SegWitWalletFeature(
            IWalletSyncManager walletSyncManager,
            SegWitWalletManager walletManager,
            IAddressBookManager addressBookManager,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            INodeStats nodeStats)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.addressBookManager = addressBookManager;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }


        public override Task InitializeAsync()
        {
            this.walletManager.Start();
            this.walletSyncManager.Start();
            this.addressBookManager.Initialize();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
            this.walletSyncManager.SyncFromHeight(1);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.walletManager.Stop();
            this.walletSyncManager.Stop();
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

        private void AddInlineStats(StringBuilder log)
        {
            if (this.walletManager is SegWitWalletManager manager)
            {
                HashHeightPair hashHeightPair = manager.LastReceivedBlockInfo();

                log.AppendLine("Wallet.Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) +
                               (manager.ContainsWallets ? hashHeightPair.Height.ToString().PadRight(8) : "No Wallet".PadRight(8)) +
                               (manager.ContainsWallets ? (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hashHeightPair.Hash) : string.Empty));
            }
        }

        private void AddComponentStats(StringBuilder log)
        {
            var walletNames = this.walletManager.GetWalletsNames().ToArray();

            if (walletNames.Length > 0)
            {
                log.AppendLine();
                log.AppendLine("======Nondeterministic Wallets======");

                foreach (string walletName in walletNames)
                {
                    var balancesPerAddress = this.walletManager.GetBalances(walletName);
                    Money confirmed = Money.Zero;
                    Money unconfirmed = Money.Zero;
                    Money spendable = Money.Zero;
                    foreach (var bal in balancesPerAddress)
                    {
                        confirmed += bal.AmountConfirmed;
                        unconfirmed += bal.AmountUnconfirmed;
                        spendable += bal.SpendableAmount;
                    }

                    log.AppendLine(($"{walletName}" + ",").PadRight(LoggingConfiguration.ColumnLength + 10)
                                   + (" Confirmed balance: " + confirmed.ToString()).PadRight(LoggingConfiguration.ColumnLength + 20)
                                   + " Unconfirmed balance: " + unconfirmed.ToString().PadRight(LoggingConfiguration.ColumnLength + 20)
                                   + " Spendable balance " + spendable.ToString()
                                   );

                    //foreach (HdAccount account in this.walletManager.GetAccounts(walletName))
                    //{
                    //    AccountBalance accountBalance = this.walletManager.GetBalances(walletName, account.Name).Single();
                    //    log.AppendLine(($"{walletName}/{account.Name}" + ",").PadRight(LoggingConfiguration.ColumnLength + 10)
                    //                   + (" Confirmed balance: " + accountBalance.AmountConfirmed.ToString()).PadRight(LoggingConfiguration.ColumnLength + 20)
                    //                   + " Unconfirmed balance: " + accountBalance.AmountUnconfirmed.ToString());
                    //}
                }
            }
        }
    }
}
