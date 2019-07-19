using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using VisualCrypt.VisualCryptLight;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class X1WalletApiFeature : Stratis.Bitcoin.Builder.Feature.FullNodeFeature
    {
        const string ServerAuthKeyFilename = "secureapiauthkey.json";
        readonly WalletController walletController;
        readonly DataFolder dataFolder;
        readonly ILogger logger;

        public X1WalletApiFeature(WalletController walletController, DataFolder dataFolder, ILoggerFactory loggerFactory)
        {
            this.walletController = walletController;
            this.dataFolder = dataFolder;
            this.logger = loggerFactory.CreateLogger(typeof(X1WalletApiFeature));
        }


        public override Task InitializeAsync()
        {
            var authKeyFile = Path.Combine(this.dataFolder.RootPath, ServerAuthKeyFilename);
            if (File.Exists(authKeyFile))
            {
                var existing = File.ReadAllText(authKeyFile);
                ECKeyPair existingAuthKey = JsonConvert.DeserializeObject<ECKeyPair>(existing);
                SecureApiControllerBase.AuthKey = existingAuthKey;
                this.logger.LogInformation($"Using existing authentication key from {authKeyFile}.");
                return Task.CompletedTask;
            }
                
            ECKeyPair newAuthKey = VCL.Instance().GenerateECKeyPair().Result;
            var created = JsonConvert.SerializeObject(newAuthKey);
            File.WriteAllText(authKeyFile, created);
            SecureApiControllerBase.AuthKey = newAuthKey;
            this.logger.LogInformation($"Created authentication key in {authKeyFile}.");
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
           
        }

        public override void ValidateDependencies(IFullNodeServiceProvider services)
        {
            base.ValidateDependencies(services);
        }

        /// <summary>
        /// Prints the help information on how to configure the settings to the logger.
        /// </summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            SecureApiSettings.PrintHelp(network);
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            SecureApiSettings.BuildDefaultConfigurationFile(builder, network);
        }
    }
}
