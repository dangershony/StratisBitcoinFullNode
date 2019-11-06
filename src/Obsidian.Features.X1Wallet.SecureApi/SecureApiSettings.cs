using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    /// <summary>
    /// The  SecureApi Settings.
    /// </summary>
    public class SecureApiSettings
    {
        /// <summary>The default web host binding.</summary>
        public const string SecureApiDefaultHostBinding = "http://0.0.0.0";

        /// <summary>The default listening port.</summary>
        public const int SecureApiDefaultPort = 37777;

        /// <summary>
        /// Default for whether SecureApi is enabled.
        /// </summary>
        public const bool IsSecureApiEnabledDefaultValue = true;

        /// <summary>
        /// Host, e.g. 'http://0.0.0.0' for remote access or 'http://localhost' for local-only access. Default: SecureApiDefaultHostBinding.
        /// </summary>
        public string SecureApiHostBinding { get; set; }

        /// <summary>
        /// Port. Default: SecureApiDefaultPort.
        /// </summary>
        public int SecureApiPort { get; set; }

        /// <summary>
        /// Whether SecureApi is enabled. Default: false.
        /// </summary>
        public bool IsSecureApiEnabled { get; set; }

        /// <summary>
        /// SecureApi user name. Default: empty string. If default, it will be assumed credentials are not required.
        /// </summary>
        public string SecureApiUser { get; set; }

        /// <summary>
        /// SecureApi password. Default: empty string. If default, it will be assumed credentials are not required.
        /// </summary>
        public string SecureApiPassword { get; set; }

        /// <summary>
        /// Initializes an instance of the object from the node configuration.
        /// </summary>
        /// <param name="nodeSettings">The node configuration.</param>
        public SecureApiSettings(NodeSettings nodeSettings)
        {
            Guard.NotNull(nodeSettings, nameof(nodeSettings));

            var logger = nodeSettings.LoggerFactory.CreateLogger(typeof(SecureApiSettings).FullName);

            TextFileConfiguration config = nodeSettings.ConfigReader;

            this.SecureApiHostBinding = config.GetOrDefault(nameof(this.SecureApiHostBinding).ToLowerInvariant(), SecureApiDefaultHostBinding, logger);

            this.SecureApiPort = config.GetOrDefault(nameof(this.SecureApiPort).ToLowerInvariant(), SecureApiDefaultPort, logger);

            this.IsSecureApiEnabled = config.GetOrDefault(nameof(this.IsSecureApiEnabled).ToLowerInvariant(), IsSecureApiEnabledDefaultValue, logger);

            this.SecureApiUser = config.GetOrDefault(nameof(this.SecureApiUser).ToLowerInvariant(), "", logger);

            this.SecureApiPassword = config.GetOrDefault(nameof(this.SecureApiPassword).ToLowerInvariant(), "", logger);
        }

        /// <summary>
        /// Prints the help information on how to configure the settings to the logger.
        /// </summary>
        /// <param name="network">The network to use.</param>
        public static void PrintHelp(Network network)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"-secureapihostbinding=<string>    Host, e.g. 'http://0.0.0.0' for remote access or 'http://localhost' for local-only access. Default: '{ SecureApiDefaultHostBinding }'.");
            builder.AppendLine($"-secureapiport=<0-65535>          Port. Default: { SecureApiDefaultPort }.");
            builder.AppendLine($"-issecureapienabled=<bool>        Whether SecureApi is enabled. Default: {SecureApiDefaultPort}.");
            builder.AppendLine($"-secureapiuser=<string>           SecureApi user name. Default: empty string. If default, it will be assumed credentials are not required.");
            builder.AppendLine($"-secureapipassword=<string>       SecureApi password. Default: empty string. If default, it will be assumed credentials are not required.");

            NodeSettings.Default(network).Logger.LogInformation(builder.ToString());
        }

        /// <summary>
        /// Get the default configuration.
        /// </summary>
        /// <param name="builder">The string builder to add the settings to.</param>
        /// <param name="network">The network to base the defaults off.</param>
        public static void BuildDefaultConfigurationFile(StringBuilder builder, Network network)
        {
            builder.AppendLine("####SECUREAPI Settings####");

            builder.AppendLine($"#Host, e.g. 'http://0.0.0.0' for remote access or 'http://localhost' for local-only access. Default: '{ SecureApiDefaultHostBinding }'.");
            builder.AppendLine($"#secureapihostbinding={SecureApiDefaultHostBinding}");

            builder.AppendLine($"#Port. Default: { SecureApiDefaultPort }.");
            builder.AppendLine($"#secureapiport={SecureApiDefaultPort}");

            builder.AppendLine($"#Whether SecureApi is enabled. Default: {IsSecureApiEnabledDefaultValue}.");
            builder.AppendLine($"#issecureapienabled={IsSecureApiEnabledDefaultValue}");

            builder.AppendLine($"#SecureApi user name. Default: empty string. If default, it will be assumed credentials are not required.");
            builder.AppendLine($"#secureapiuser=");

            builder.AppendLine($"#SecureApi password. Default: empty string. If default, it will be assumed credentials are not required.");
            builder.AppendLine($"#secureapipassword=");
        }
    }
}
