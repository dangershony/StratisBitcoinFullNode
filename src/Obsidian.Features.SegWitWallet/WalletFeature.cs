using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet
{
    /// <inheritdoc />
    public class WalletFeature : BaseWalletFeature
    {
        readonly WalletManagerWrapper walletManagerWrapper;
        readonly IConnectionManager connectionManager;
        readonly IAddressBookManager addressBookManager;
        readonly BroadcasterBehavior broadcasterBehavior;

        public WalletFeature(
            WalletManagerWrapper walletManagerFacade,
            //IWalletSyncManager walletSyncManager,
            IAddressBookManager addressBookManager,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            INodeStats nodeStats)
        {
            this.walletManagerWrapper = (WalletManagerWrapper)walletManagerFacade;
            //this.walletSyncManager = walletSyncManager;
            this.addressBookManager = addressBookManager;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }


        public override Task InitializeAsync()
        {
            //this.walletManagerFacade.Start();
            // this.walletSyncManager.Start(); Do not call this here!!!!
            //this.addressBookManager.Initialize();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.walletManagerWrapper.Dispose();
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

        private void AddInlineStats(StringBuilder log)
        {
            var context = this.walletManagerWrapper.GetWalletContext(null, true);

            if (context != null)
            {
                var height = context.WalletManager.WalletLastBlockSyncedHeight.ToString();
                var hash = context.WalletManager.WalletLastBlockSyncedHash?.ToString() ?? "n/a";

                log.AppendLine($"Wallet {context.WalletManager.WalletName}: Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + height.PadRight(8) +
                               (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hash));
            }
            else
            {
                log.AppendLine("No wallet loaded.");
            }
        }

        private void AddComponentStats(StringBuilder log)
        {
            var context = this.walletManagerWrapper.GetWalletContext(null, true);

            if (context == null)
            {
                log.AppendLine();
                log.AppendLine("======Nondeterministic Wallets======");
                log.AppendLine("No wallet loaded.");
                return;
            }

            var walletNames = new[] { context.WalletManager.WalletName };

            if (walletNames.Length > 0)
            {
                log.AppendLine();
                log.AppendLine("======Nondeterministic Wallets======");

                foreach (string walletName in walletNames)
                {
                    var balancesPerAddress = context.WalletManager.GetBalances();
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

                    //foreach (HdAccount account in this.walletManagerFacade.GetAccounts(walletName))
                    //{
                    //    AccountBalance accountBalance = this.walletManagerFacade.GetBalances(walletName, account.Name).Single();
                    //    log.AppendLine(($"{walletName}/{account.Name}" + ",").PadRight(LoggingConfiguration.ColumnLength + 10)
                    //                   + (" Confirmed balance: " + accountBalance.AmountConfirmed.ToString()).PadRight(LoggingConfiguration.ColumnLength + 20)
                    //                   + " Unconfirmed balance: " + accountBalance.AmountUnconfirmed.ToString());
                    //}
                }
            }
        }
    }
}
