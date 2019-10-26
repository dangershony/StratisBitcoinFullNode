using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models.Api.Responses;
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
        WalletController walletController;

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

        string height = "n/a";
        string hash = "n/a";
        string walletName;

        void AddInlineStats(StringBuilder log)
        {
            if (this.walletName != null)
                log.AppendLine($"Wallet {this.walletName}: Height: ".PadRight(LoggingConfiguration.ColumnLength + 1) + this.height.PadRight(8) +
                               (" Wallet.Hash: ".PadRight(LoggingConfiguration.ColumnLength - 1) + this.hash));
            else
                log.AppendLine("No wallet loaded.");
        }

        void AddComponentStats(StringBuilder log)
        {
            string loadedWalletName;

            using (var context = this.walletManagerFactory.GetWalletContext(null, true))
            {
                if (context == null)
                    loadedWalletName = null;
                else
                    loadedWalletName = context.WalletManager.WalletName;
            }

            WalletInformation walletInformation = null;
            if (loadedWalletName != null)
            {
                this.walletController.SetWalletName(loadedWalletName, true);
                walletInformation = this.walletController.GetWalletInfo();
            }
               

            if (walletInformation == null)
            {
                log.AppendLine();
                log.AppendLine("====== X1 Wallet ======");
                log.AppendLine("No wallet file loaded.");

                // for inline stats
                this.walletName = null;
                this.hash = "n/a";
                this.height = "n/a";
                return;
            }

            var output = Serializer.Print(walletInformation);
            var header = $" X1 Wallet v. {walletInformation.AssemblyVersion} ";
            output = output.Replace(nameof(WalletInformation), header);
            log.AppendLine();
            log.Append(output);

            // for inline stats
            this.walletName = walletInformation.WalletName;
            this.hash = walletInformation.SyncedHash?.ToString() ?? "n/a";
            this.height = walletInformation.SyncedHeight.ToString();
        }

        public override void Dispose()
        {
            base.Dispose();
            this.walletManagerFactory.Dispose();
        }
    }
}
