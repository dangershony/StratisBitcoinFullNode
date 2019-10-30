using System;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Tools;
using Stratis.Bitcoin.Configuration.Logging;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet.Feature
{
    /// <inheritdoc />
    public class X1WalletFeature : BaseWalletFeature
    {
        readonly WalletManagerFactory walletManagerFactory;
        readonly IConnectionManager connectionManager;
        readonly BroadcasterBehavior broadcasterBehavior;
        readonly Network network;
        readonly WalletController walletController;

        string inlineStats = NoWalletLoaded;
        const string NoWalletLoaded = "No wallet loaded.";

        public X1WalletFeature(
            WalletManagerFactory walletManagerFactory,
            IConnectionManager connectionManager,
            BroadcasterBehavior broadcasterBehavior,
            INodeStats nodeStats, Network network, WalletController walletController)
        {
            this.walletManagerFactory = walletManagerFactory;
            this.connectionManager = connectionManager;
            this.broadcasterBehavior = broadcasterBehavior;
            this.network = network;
            this.walletController = walletController;

            nodeStats.RegisterStats(AddComponentStats, StatsType.Component, GetType().Name);
            nodeStats.RegisterStats(AddInlineStats, StatsType.Inline, GetType().Name, 800);
        }

        public override Task InitializeAsync()
        {
            IsDefaultBlockHashExtension.Init(this.network);

            this.connectionManager.Parameters.TemplateBehaviors.Add(this.broadcasterBehavior);

            return Task.CompletedTask;
        }

        void AddInlineStats(StringBuilder log)
        {
            log.AppendLine(this.inlineStats);
        }

        void AddComponentStats(StringBuilder log)
        {
            WriteDaemonInfo(log);

            using (var context = this.walletManagerFactory.GetWalletContext(null, true))
            {
                string loadedWalletName = context?.WalletManager.WalletName;
                this.walletController.SetWalletName(loadedWalletName, true);
            }

            WalletInfo walletInfo = this.walletController.GetWalletInfo();

            log.AppendLine();
            var header = $" {walletInfo.AssemblyName} {walletInfo.AssemblyVersion} ";
            var output = Serializer.Print(walletInfo, header);
            log.Append(output);

            if (walletInfo.WalletDetails != null)
            {
                this.inlineStats =
                    $"Wallet {walletInfo.WalletDetails.WalletName}: Height: ".PadRight(
                        LoggingConfiguration.ColumnLength + 1) +
                    walletInfo.WalletDetails.SyncedHeight.ToString().PadRight(8) +
                    (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) +
                     walletInfo.WalletDetails.SyncedHash);
            }
            else
            {
                this.inlineStats = NoWalletLoaded;
            }
            if (walletInfo.WalletDetails != null)
                WriteTransactionInfo(log);
        }

        void WriteDaemonInfo(StringBuilder log)
        {
            try
            {
                var daemonInfo = this.walletController.GetDaemonInfo();
                var header = $" {daemonInfo.ProcessName} {daemonInfo.AssemblyVersion} ";
                var output = Serializer.Print(daemonInfo, header);
                log.AppendLine();
                log.AppendLine(output);
            }
            catch (Exception e)
            {
                log.AppendLine($"Can't add other stats: {e.Message}");
            }
        }

        void WriteTransactionInfo(StringBuilder log)
        {
            var request = new HistoryRequest { Take = 3 };
            var daemonInfo = this.walletController.GetHistoryInfo(request);
            var header = $" Last {request.Take.Value} Transactions ";
            var output = Serializer.Print(daemonInfo, header);
            log.AppendLine();
            log.AppendLine(output);
        }

        public override void Dispose()
        {
            base.Dispose();
            this.walletManagerFactory.Dispose();
        }
    }
}
