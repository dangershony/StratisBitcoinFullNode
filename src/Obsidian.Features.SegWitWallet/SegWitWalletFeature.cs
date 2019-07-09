using System.Linq;
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

namespace Obsidian.Features.SegWitWallet
{
    /// <inheritdoc />
    public class SegWitWalletFeature : BaseWalletFeature
    {
        readonly IWalletSyncManager walletSyncManager;
        readonly WalletManagerFacade walletManagerFacade;
        readonly IConnectionManager connectionManager;
        readonly IAddressBookManager addressBookManager;
        readonly BroadcasterBehavior broadcasterBehavior;

        public SegWitWalletFeature(
            IWalletManager walletManagerFacade,
            IWalletSyncManager walletSyncManager,
            IAddressBookManager addressBookManager,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            NodeSettings nodeSettings,
            ILoggerFactory loggerFactory,
            INodeStats nodeStats)
        {
            this.walletManagerFacade = (WalletManagerFacade)walletManagerFacade;
            this.walletSyncManager = walletSyncManager;
            this.addressBookManager = addressBookManager;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;

            nodeStats.RegisterStats(this.AddComponentStats, StatsType.Component);
            nodeStats.RegisterStats(this.AddInlineStats, StatsType.Inline, 800);
        }


        public override Task InitializeAsync()
        {
            //this.walletManagerFacade.Start();
            //this.walletSyncManager.Start();
            //this.addressBookManager.Initialize();

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);
            //this.walletSyncManager.SyncFromHeight(1);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            //this.walletManagerFacade.Stop();
            //this.walletSyncManager.Stop();
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

        private void AddInlineStats(StringBuilder log)
        {
            var manager = this.walletManagerFacade.GetManager(null, true);

            if (manager != null)
            {
                var height = manager.Wallet.LastBlockSyncedHeight.ToString();
                var hash = manager.Wallet.LastBlockSyncedHash?.ToString() ?? "n/a";

                log.AppendLine($"Wallet {manager.Wallet.Name}: Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + height.PadRight(8) +
                               (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + hash));
            }
            else
            {
                log.AppendLine("No wallet loaded.");
            }
        }

        private void AddComponentStats(StringBuilder log)
        {
            var manager = this.walletManagerFacade.GetManager(null, true);

            if (manager == null)
            {
                log.AppendLine();
                log.AppendLine("======Nondeterministic Wallets======");
                log.AppendLine("No wallet loaded.");
                return;
            }

            var walletNames = new[] { manager.Wallet.Name };

            if (walletNames.Length > 0)
            {
                log.AppendLine();
                log.AppendLine("======Nondeterministic Wallets======");

                foreach (string walletName in walletNames)
                {
                    var balancesPerAddress = manager.GetBalances();
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
