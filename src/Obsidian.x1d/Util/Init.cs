using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using Obsidian.Features.X1Wallet.Tools;
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
                arguments.Add("datadirroot=Obsidian");

            return arguments.ToArray();
        }

        internal static void PrintWelcomeMessage(NodeSettings nodeSettings, IFullNode fullNode)
        {

            var welcome = new StringBuilder();
            welcome.AppendLine($"Welcome to Obsidian! Loading network {nodeSettings.Network.Name}...");
            welcome.AppendLine();
            welcome.AppendLine(Properties.Resources.Brand);
            welcome.AppendLine($"{GetCredits()}");
            ((FullNode) fullNode).NodeService<ILoggerFactory>().CreateLogger(GetName())
                .LogInformation(welcome.ToString());
        }

        static string GetCredits()
        {
            var sb = new StringBuilder();
            var intro = "Credits, greetings and thanks to: ";
            sb.Append(intro);

            var arr = Properties.Resources.Credits.Split(Environment.NewLine,StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).OrderBy(x => x).ToList();
            var length = intro.Length;

            foreach (var name in arr)
            {
                if (length + name.Length <= 100)
                {
                    var item = $"{name}, ";
                    length += item.Length;
                    sb.Append(item);
                }
                else
                {
                    sb.Append(Environment.NewLine);
                    length = 0;
                    var item = $"{name}, ";
                    length += item.Length;
                    sb.Append(item);
                }
            }

            var credits = sb.ToString(0, sb.Length - 2);

            return $"{credits} and many more - join the power!";
        }

        internal static string GetName()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetName().Name;
            var version = assembly.GetShortVersionString();
            // ReSharper disable once UnreachableCode
            var compilation = IsDebug ? " (Debug)" : "";
            return $"{name} {version}{compilation}";
        }

        internal static void RunIfDebugModeDelayed(IFullNode fullNode, int milliSeconds = 10000)
        {
           // #if DEBUG
            _ = Task.Run(async () =>
            {
                await Task.Delay(milliSeconds);
                TestBench.RunTestCodeAsync((FullNode)fullNode);
            });
          //  #endif
        }

#if DEBUG
        const bool IsDebug = true;
#else
		public const bool IsDebug = false;
#endif
    }
}
