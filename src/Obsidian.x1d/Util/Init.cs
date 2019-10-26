using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using Obsidian.Networks.ObsidianX;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Configuration;

namespace Obsidian.x1d.Util
{
    static class Init
    {
        internal static NodeSettings GetNodeSettings(string[] args)
        {
            var nodeSettings = new NodeSettings(networksSelector: ObsidianXNetworksSelector.Obsidian,
                protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}", args: MergeArgs(args))
            {
                MinProtocolVersion = ProtocolVersion.PROVEN_HEADER_VERSION
            };
            return nodeSettings;
        }

        static string[] MergeArgs(string[] args)
        {
            var arguments = new List<string>();
            if (args != null)
                arguments.AddRange(args);

            bool isDataDirRootProvided = false;
            foreach (var a in arguments)
            {
                if (a.ToLowerInvariant().Contains("datadirroot"))
                    isDataDirRootProvided = true;
            }
            if (!isDataDirRootProvided)
                arguments.Add("datadirroot=ObsidianX");

            return arguments.ToArray();
        }

        internal static void PrintWelcomeMessage(NodeSettings nodeSettings, IFullNode fullNode)
        {
            var welcome = new StringBuilder();
            welcome.AppendLine("Welcome to ObsidianX!");
            welcome.AppendLine();
            welcome.AppendLine($"Initializing network {nodeSettings.Network.Name}...");
            welcome.AppendLine(Properties.Resources.Brand);
            ((FullNode) fullNode).NodeService<ILoggerFactory>().CreateLogger(GetName())
                .LogInformation(welcome.ToString());
        }

        internal static string GetName()
        {
            var assembly = Assembly.GetExecutingAssembly().GetName();
            var name = assembly.Name;
            var version = assembly.Version;
            // ReSharper disable once UnreachableCode
            var compilation = IsDebug ? " (Debug)" : "";
            return $"{name} {version}{compilation}";
        }

        internal static void RunIfDebugMode(IFullNode fullNode)
        {
            //#if DEBUG
            _ = Task.Run(async () =>
            {
                await Task.Delay(0);
                TestBench.RunTestCodeAsync((FullNode)fullNode);  // start mining to the wallet
            });
            //#endif
        }

#if DEBUG
        const bool IsDebug = true;
#else
		public const bool IsDebug = false;
#endif
    }
}
