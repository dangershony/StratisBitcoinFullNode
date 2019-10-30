using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Api.Responses;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Tools;
using Obsidian.Features.X1Wallet.Transactions;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;

namespace Obsidian.Features.X1Wallet
{
    public class WalletController : Controller
    {
        readonly WalletManagerFactory walletManagerFactory;
        readonly Network network;
        readonly IConnectionManager connectionManager;
        readonly ChainIndexer chainIndexer;
        readonly IBroadcasterManager broadcasterManager;
        readonly IDateTimeProvider dateTimeProvider;

        readonly IFullNode fullNode;
        readonly NodeSettings nodeSettings;
        readonly IChainState chainState;
        readonly INetworkDifficulty networkDifficulty;
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;

        string walletName;

        WalletContext GetWalletContext()
        {
            return this.walletManagerFactory.GetWalletContext(this.walletName);
        }

        TransactionService GetTransactionHandler()
        {
            return new TransactionService(this.loggerFactory, this.walletManagerFactory, this.walletName, this.network);
        }

        public WalletController(
            WalletManagerFactory walletManagerFactory,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider,
            IFullNode fullNode,
            NodeSettings nodeSettings,
            IChainState chainState,
            INetworkDifficulty networkDifficulty,
            ILoggerFactory loggerFactory
            )
        {
            this.walletManagerFactory = walletManagerFactory;
            this.connectionManager = connectionManager;
            this.network = network;
            this.chainIndexer = chainIndexer;
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
            this.chainState = chainState;
            this.networkDifficulty = networkDifficulty;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(typeof(WalletController).Name);
        }

        public HistoryInfo GetHistoryInfo(HistoryRequest historyRequest)
        {
            using var context = GetWalletContext();
            return context.WalletManager.GetHistoryInfo(historyRequest);
        }

        public void SetWalletName(string targetWalletName, bool canReuseInstance = false)
        {
            if (this.walletName != null && !canReuseInstance)
                throw new InvalidOperationException("walletName is already set - this controller must be a new instance per request!");
            this.walletName = targetWalletName;
        }

        public LoadWalletResponse LoadWallet()
        {
            using var context = GetWalletContext();
            return context.WalletManager.LoadWallet();
        }

        public void CreateWallet(WalletCreateRequest walletCreateRequest)
        {
            this.walletManagerFactory.CreateWallet(walletCreateRequest);
        }

        public ImportKeysResponse ImportKeys(ImportKeysRequest importKeysRequest)
        {
            using var context = GetWalletContext();
            return context.WalletManager.ImportKeys(importKeysRequest);
        }

        public ExportKeysResponse ExportKeys(ExportKeysRequest importKeysRequest)
        {
            using var context = GetWalletContext();
            return context.WalletManager.ExportKeys(importKeysRequest);

        }



        public void StartStaking(StartStakingRequest startStakingRequest)
        {
            using var context = GetWalletContext();
            context.WalletManager.StartStaking(startStakingRequest.Passphrase);
        }

        public void StopStaking()
        {
            using var context = GetWalletContext();
            context.WalletManager.StopStaking();
        }

        public Balance GetBalance()
        {
            using var context = GetWalletContext();
            context.WalletManager.GetBudget(out var balance);
            return balance;
        }

        public long EstimateFee(TransactionRequest request)
        {
            request.Sign = false;
            var response = BuildTransaction(request);
            return response.Fee;
        }

        public TransactionResponse BuildSplitTransaction(TransactionRequest request)
        {
            var count = 1000;
            var amount = Money.Coins(5000);

            using var walletContext = GetWalletContext();

            walletContext.WalletManager.GetBudget(out Balance _);
            var recipients = walletContext.WalletManager.GetAllAddresses().Values.Take(count).Select(x => new Recipient { Address = x.Address, Amount = amount }).ToList();
            TransactionResponse response = GetTransactionHandler().BuildTransaction(recipients, request.Sign, request.Passphrase);
            if (request.Send)
            {
                this.broadcasterManager.BroadcastTransactionAsync(response.Transaction).GetAwaiter().GetResult();
            }
            else
            {
                response.BroadcastState = BroadcastState.NotRequested;
            }
            return response;
        }

        public TransactionResponse BuildTransaction(TransactionRequest request)
        {
            int retries = 0;

            while (!CanBuildTx())
            {
                if (retries < 15)
                {
                    Task.Delay(100).Wait();
                    retries++;
                }
                else
                {
                    throw new X1WalletException(HttpStatusCode.BadRequest,
                        "The wallet is not fully synced yet, please retry later.");
                }
            }

            var response = GetTransactionHandler().BuildTransaction(request.Recipients, request.Sign, request.Passphrase, request.TransactionTimestamp, request.Burns);

            if (request.Send)
            {
                if (!this.connectionManager.ConnectedPeers.Any())
                {
                    throw new X1WalletException(HttpStatusCode.BadRequest,"Can't send the transactions without connections.");
                }
                this.broadcasterManager.BroadcastTransactionAsync(response.Transaction).GetAwaiter().GetResult();
                TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(response.Transaction.GetHash());

                if (transactionBroadCastEntry.State == State.CantBroadcast)
                {
                    throw new X1WalletException(HttpStatusCode.InternalServerError,
                        $"Can't send the transaction: {transactionBroadCastEntry.ErrorMessage}.");
                }

                response.BroadcastState = transactionBroadCastEntry.State.ToBroadcastState();
            }
            else
            {
                response.BroadcastState = BroadcastState.NotRequested;
            }
            return response;
        }

        bool CanBuildTx()
        {
            try
            {
                using var context = GetWalletContext();
                return context.WalletManager.WalletLastBlockSyncedHash == this.chainIndexer.Tip.HashBlock &&
                       this.chainIndexer.Tip?.Height > 0;
            }
            catch (Exception e)
            {
                this.logger.LogError(e.Message);
                return false;
            }
        }

        public GetAddressesResponse GetUnusedReceiveAddresses()
        {
            using (var context = GetWalletContext())
            {
                var p2WpkhAddress = context.WalletManager.GetUnusedAddress();
                bool isUsed = false;
                if (p2WpkhAddress == null)
                {
                    p2WpkhAddress = context.WalletManager.GetAllAddresses().First().Value;
                    isUsed = true;
                }

                var model = new GetAddressesResponse { Addresses = new List<AddressModel>() };
                model.Addresses.Add(new AddressModel { Address = p2WpkhAddress.Address, IsUsed = isUsed, FullAddress = p2WpkhAddress });
                return model;
            }
        }


        public void Repair(RepairRequest date)
        {
            var chainedHeader = this.chainIndexer.GetHeader(1);
            using var context = GetWalletContext();
            context.WalletManager.RemoveBlocks(chainedHeader);
        }

        public DaemonInfo GetDaemonInfo()
        {
            var process = Process.GetCurrentProcess();
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            Debug.Assert(assembly != null);
            return new DaemonInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                ProcessMemory = process.PrivateMemorySize64,
                MachineName = Environment.MachineName,
                CodeBase = new Uri(assembly.GetName().CodeBase).AbsolutePath,
                AssemblyVersion = assembly.GetShortVersionString(),
                AgentName = this.connectionManager.ConnectionSettings.Agent,
                StartupTime = new DateTimeOffset(this.fullNode.StartTime).ToUnixTimeSeconds(),
                NetworkName = this.network.Name,
                CoinTicker = this.network.CoinTicker,
                Testnet = this.network.IsTest(),
                MinTxFee = this.network.MinTxFee,
                MinTxRelayFee = this.network.MinRelayTxFee,
                Features = this.fullNode.Services.Features.Select(x => new StringItem { NamedItem = $"{x.GetType()}, v.{x.GetType().Assembly.GetShortVersionString()}" }).ToArray(),
                WalletPath = new Uri(this.nodeSettings.DataFolder.WalletPath).AbsolutePath,
                WalletFiles = Directory.EnumerateFiles(this.nodeSettings.DataFolder.WalletPath, $"*{X1WalletFile.FileExtension}", SearchOption.TopDirectoryOnly)
                    .Select(x => new StringItem { NamedItem = Path.GetFileName(x) }).ToArray()
            };
        }


        public WalletInfo GetWalletInfo()
        {
            var syncingInfo = new WalletInfo
            {
                ConsensusTipHeight = this.chainState.ConsensusTip.Height,
                ConsensusTipHash = this.chainState.ConsensusTip.HashBlock,
                ConsensusTipAge = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.chainState.ConsensusTip.Header.Time),
                MaxTipAge = this.network.MaxTipAge,
                AssemblyName = typeof(WalletController).Assembly.GetName().Name,
                AssemblyVersion = typeof(WalletController).Assembly.GetShortVersionString(),
                IsAtBestChainTip = this.chainState.IsAtBestChainTip
            };

            if (this.chainState.BlockStoreTip != null)
            {
                syncingInfo.BlockStoreHeight = this.chainState.BlockStoreTip.Height;
                syncingInfo.BlockStoreHash = this.chainState.BlockStoreTip.HashBlock;
            }

            syncingInfo.ConnectionInfo = GetConnections();

            if (this.walletName != null)
            {
                syncingInfo.WalletDetails = GetWalletDetails();
            }

            return syncingInfo;
        }

        ConnectionInfo GetConnections()
        {
            const string notAvailable = "n/a";
            var info = new ConnectionInfo { Peers = new List<PeerInfo>() };
            info.BestPeerHeight = 0;

            foreach (var p in this.connectionManager.ConnectedPeers)
            {
                var behavior = p.Behavior<ConsensusManagerBehavior>();
                var peer = new PeerInfo
                {
                    Version = p.PeerVersion != null ? p.PeerVersion.UserAgent : notAvailable,
                    RemoteSocketEndpoint = p.RemoteSocketEndpoint != null ? p.RemoteSocketEndpoint.ToString() : notAvailable,
                    BestReceivedTipHeight = behavior != null && behavior.BestReceivedTip != null ? behavior.BestReceivedTip.Height : 0,
                    BestReceivedTipHash = behavior != null && behavior.BestReceivedTip != null ? behavior.BestReceivedTip.HashBlock : null,
                    IsInbound = p.Inbound
                };

                if (peer.BestReceivedTipHeight > info.BestPeerHeight)
                {
                    info.BestPeerHeight = peer.BestReceivedTipHeight;
                    info.BestPeerHash = peer.BestReceivedTipHash;
                }
                if (peer.IsInbound)
                    info.InBound++;
                else
                    info.OutBound++;
                info.Peers.Add(peer);
            }
            return info;
        }

        WalletDetails GetWalletDetails()
        {
            using var context = GetWalletContext();
            return context.WalletManager.GetWalletDetails();
        }

    }
}
