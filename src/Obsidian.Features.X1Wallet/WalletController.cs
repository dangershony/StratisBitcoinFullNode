using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Api.Responses;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;
using Obsidian.Features.X1Wallet.Transactions;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using AddressModel = Obsidian.Features.X1Wallet.Models.Api.AddressModel;
using BuildTransactionRequest = Obsidian.Features.X1Wallet.Transactions.BuildTransactionRequest;
using Recipient = Obsidian.Features.X1Wallet.Transactions.Recipient;

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
        }

        public void SetWalletName(string targetWalletName)
        {
            if (this.walletName != null)
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
            context.WalletManager.StartStaking(startStakingRequest.Password);
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

        public long EstimateFee(BuildTransactionRequest request)
        {
            request.Sign = false;
            var response = BuildTransaction(request);
            return response.Fee;
        }

        public BuildTransactionResponse BuildSplitTransaction(BuildTransactionRequest request)
        {
            var count = 1000;
            var amount = Money.Coins(5000);

            using var walletContext = GetWalletContext();

            walletContext.WalletManager.GetBudget(out Balance _);
            var recipients = walletContext.WalletManager.GetAllAddresses().Values.Take(count).Select(x => new Recipient { Address = x.Address, Amount = amount }).ToList();
            BuildTransactionResponse response = GetTransactionHandler().BuildTransaction(recipients, true, request.Passphrase);
            return response;
        }

        public BuildTransactionResponse BuildTransaction(BuildTransactionRequest request)
        {
            var response = GetTransactionHandler().BuildTransaction(request.Recipients, request.Sign, request.Passphrase, request.TransactionTimestamp, request.Burns);
            return response;
        }


        public WalletSendTransactionModel SendTransaction(SendHexTransactionRequest request)
        {
            if (!this.connectionManager.ConnectedPeers.Any())
            {
                throw new X1WalletException(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", new Exception());
            }

            Transaction transaction = this.network.CreateTransaction(request.Hex);

            var model = new WalletSendTransactionModel
            {
                TransactionId = transaction.GetHash(),
                Outputs = new List<TransactionOutputModel>()
            };

            foreach (TxOut output in transaction.Outputs)
            {
                bool isUnspendable = output.ScriptPubKey.IsUnspendable;
                model.Outputs.Add(new TransactionOutputModel
                {
                    Address = isUnspendable ? null : output.ScriptPubKey.GetDestinationAddress(this.network)?.ToString(),
                    Amount = output.Value,
                    OpReturnData = isUnspendable ? Encoding.UTF8.GetString(output.ScriptPubKey.ToOps().Last().PushData) : null
                });
            }

            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry.State == State.CantBroadcast)
            {
                throw new X1WalletException(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, new Exception("Transaction Exception"));
            }

            return model;

        }

        public GetWalletInfoResponse GetWalletInfo()
        {
            using var context = GetWalletContext();
            return context.WalletManager.GetWalletInfo();
        }

        public WalletFileModel ListWalletsFiles()
        {
            (string folderPath, IEnumerable<string> filesNames) result = this.walletManagerFactory.GetWalletsFiles();
            var model = new WalletFileModel
            {
                WalletsPath = result.folderPath,
                WalletsFiles = result.filesNames
            };
            return model;
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

        public void SyncFromDate(WalletSyncFromDateRequest request)
        {
            this.walletManagerFactory.WalletSyncManagerSyncFromDate(request.Date);
        }

        public ConnectionInfo GetConnections()
        {
            var info = new ConnectionInfo { Peers = new List<PeerInfo>() };
            info.BestPeerHeight = 0;
            foreach (var p in this.connectionManager.ConnectedPeers)
            {
                var behavior = p.Behavior<ConsensusManagerBehavior>();
                var peer = new PeerInfo
                {
                    Version = p.PeerVersion != null ? p.PeerVersion.UserAgent : "n/a",
                    RemoteSocketEndpoint = p.RemoteSocketEndpoint.ToString(),
                    BestReceivedTipHeight = behavior.BestReceivedTip.Height,
                    BestReceivedTipHash = behavior.BestReceivedTip.HashBlock,
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

        SyncingInfo GetSyncingInfo()
        {
            var syncingInfo= new SyncingInfo
            {
                ConsensusTipHeight = this.chainState.ConsensusTip.Height,
                ConsensusTipHash = this.chainState.ConsensusTip.HashBlock,
                ConsensusTipAge = (int) (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.chainState.ConsensusTip.Header.Time),
                MaxTipAge = this.network.MaxTipAge,
                IsAtBestChainTip = this.chainState.IsAtBestChainTip
            };

            if (this.chainState.BlockStoreTip != null)
            {
                syncingInfo.BlockStoreHeight = this.chainState.BlockStoreTip.Height;
                syncingInfo.BlockStoreHash = this.chainState.BlockStoreTip.HashBlock;
            }

            if (this.walletManagerFactory.GetWalletContext(null, true) != null)
            {
                using (var context = GetWalletContext())
                {
                    syncingInfo.WalletTipHeight = context.WalletManager.WalletLastBlockSyncedHeight;
                    syncingInfo.WalletTipHash = context.WalletManager.WalletLastBlockSyncedHash;
                    syncingInfo.WalletName = context.WalletManager.WalletName;
                }
            }

            syncingInfo.ConnectionInfo = GetConnections();
          
            return syncingInfo;
        }

        public NodeInfo GetNodeInfo()
        {
            var process = Process.GetCurrentProcess();

            return new NodeInfo
            {
                ProcessId = process.Id,
                ProcessName = process.ProcessName,
                MachineName = process.MachineName,
                Program = Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().GetName().CodeBase),
                Agent = this.connectionManager.ConnectionSettings.Agent,
                StartupTime = new DateTimeOffset(this.fullNode.StartTime).ToUnixTimeSeconds(),
                NetworkName = this.network.Name,
                CoinTicker = this.network.CoinTicker,
                Testnet = this.network.IsTest(),
                MinTxFee = this.network.MinTxFee,
                MinTxRelayFee = this.network.MinRelayTxFee,
                DataDirectoryPath = this.nodeSettings.DataDir,
                Features = this.fullNode.Services.Features.Select(x => $"{x.GetType()}, v.{x.GetType().Assembly.GetName().Version}").ToArray(),
            };
        }


        public StakingInfo GetStakingInfo()
        {
            using var context = GetWalletContext();
            return context.WalletManager.GetStakingInfo();
        }
    }
}
