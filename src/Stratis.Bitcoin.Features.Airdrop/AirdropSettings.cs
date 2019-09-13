using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.Airdrop
{
    /// <summary>
    /// Configuration related to the the airdrop feature.
    /// </summary>
    public class AirdropSettings
    {
        /// <summary>Defines block height at wich to create the airdrop.</summary>
        public int? SnapshotHeight { get; set; }

        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public AirdropSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));
            
            this.logger = nodeSettings.LoggerFactory.CreateLogger(typeof(AirdropSettings).FullName);            
            TextFileConfiguration config = nodeSettings.ConfigReader;
            this.SnapshotHeight = config.GetOrDefault<int>("snapshotheight", 0, this.logger);
        }

        /// <summary>Prints the help information on how to configure the DNS settings to the logger.</summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-snapshotheight=<1-max>   The height of the chain to take the snapshot form");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####Airdrop Settings####");
            builder.AppendLine($"#The SnapshotHeight. Defaults to 0 (disabled)");
        }
    }
}
