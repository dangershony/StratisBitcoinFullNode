﻿using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Apps;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.RPC;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.ObsidianD
{
	public static class Program
	{
		
		public static void Main(string[] args)
		{
			MainAsync(args).Wait();
		}

		/// <summary> Starts the fullnode asyncronously.</summary>
		/// <remarks>To run as gateway node use the args -gateway=1 -whitelist=[trusted-QT-ip] addnode=[trusted-QT-ip],
		/// e.g. -gateway=1 -whitelist=104.45.21.229 addnode=104.45.21.229 -port=56666
		/// Use arg -maxblkmem=2 on a low memory VPS.
		/// </remarks>
		/// <param name="args">args</param>
		/// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
		static async Task MainAsync(string[] args)
		{
			try
			{
				var nodeSettings = new NodeSettings(networksSelector: ObsidianNetworksSelector.Obsidian,
					protocolVersion: ProtocolVersion.PROVEN_HEADER_VERSION, agent: $"{GetName()}, StratisNode", args: args)
				{
					MinProtocolVersion =ProtocolVersion.ALT_PROTOCOL_VERSION
				};
#if DEBUG
				nodeSettings.Logger.LogWarning($"Running {GetName()} in DEBUG configuration.");
#endif
				
				IFullNode node = new FullNodeBuilder()
					.UseNodeSettings(nodeSettings)
					.UseBlockStore()
					.UsePosConsensus()
					.UseMempool()
					.UseColdStakingWallet()
					.AddPowPosMining()
					.UseApi()
					.UseApps()
					.AddRPC()
					.Build();

				if (node != null)
					await node.RunAsync();
			}
			catch (Exception ex)
			{
				Console.WriteLine(@"There was a problem initializing the node. Details: '{0}'", ex.Message);
			}
		}

		static string GetName()
		{
			return $"ObsidianD {Assembly.GetEntryAssembly()?.GetName().Version}";
		}
	}
}
