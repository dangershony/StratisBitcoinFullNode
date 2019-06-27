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
    public class SegWitWalletApiFeature : BaseWalletFeature
    {
        readonly SegWitWalletController segWitWalletController;


        public SegWitWalletApiFeature(
            SegWitWalletController segWitWalletController,
            ILoggerFactory loggerFactory
          )
        {
            this.segWitWalletController = segWitWalletController;
           
        }


        public override Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
           
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

       
    }
}
