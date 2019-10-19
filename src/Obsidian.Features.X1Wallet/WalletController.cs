using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Api;
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
using BuildTransactionRequest = Obsidian.Features.X1Wallet.Models.Api.BuildTransactionRequest;
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

        readonly StakingCore posMinting;

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
            StakingCore posMinting,
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
            this.posMinting = posMinting;
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

        public WalletGeneralInfoModel GetGeneralInfo()
        {
            using var context = GetWalletContext();
            var manager = context.WalletManager;

            var model = new WalletGeneralInfoModel
            {
                Network = this.network,
                CreationTime = Utils.UnixTimeToDateTime(this.network.GenesisTime),
                LastBlockSyncedHeight = manager.WalletLastBlockSyncedHeight,
                ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                ChainTip = this.chainIndexer.Tip.Height,
                IsChainSynced = this.chainIndexer.IsDownloaded(),
                IsDecrypted = this.posMinting.GetGetStakingInfoModel().Enabled,
                WalletFilePath = manager.CurrentX1WalletFilePath
            };

            return model;
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

        public Money EstimateFee(TxFeeEstimateRequest request)
        {
            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                recipients.Add(new Recipient
                {
                    ScriptPubKey = recipientModel.DestinationAddress.ScriptPubKeyFromBech32Safe(),
                    Amount = recipientModel.Amount
                });
            }

            var burn = request.OpReturnData != null
                ? new Burn { Utf8String = request.OpReturnData, Amount = request.OpReturnAmount }
                : null;

            var response = GetTransactionHandler().BuildTransaction(recipients, false, burn: burn);

            return response.Fee;
        }

        public BuildTransactionResponse BuildSplitTransaction(BuildTransactionRequest request)
        {
            var count = 1000;
            var amount = Money.Coins(5000);
            var recipients = new List<Recipient>(count);

            using (var walletContext = GetWalletContext())
            {
                walletContext.WalletManager.GetBudget(out Balance _);
                IEnumerable<NBitcoin.Script> destinations = walletContext.WalletManager.GetAllAddresses().Select(kvp => kvp.Value.ScriptPubKeyFromPublicKey()).Take(count);
                foreach (var d in destinations)
                {
                    recipients.Add(new Recipient
                    { ScriptPubKey = d, Amount = amount });
                }
            }
            BuildTransactionResponse response = GetTransactionHandler().BuildTransaction(recipients, true, request.Password);

            return response;
        }

        public BuildTransactionResponse BuildTransaction(BuildTransactionRequest request)
        {
            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                recipients.Add(new Recipient
                {
                    ScriptPubKey = recipientModel.DestinationAddress.ScriptPubKeyFromBech32Safe(),
                    Amount = recipientModel.Amount
                });
            }

            var response = GetTransactionHandler().BuildTransaction(recipients, sign: request.Sign, request.Password,
                burn: request.OpReturnData != null ? new Burn { Utf8String = request.OpReturnData, Amount = request.OpReturnAmount } : null);

            return response;
        }


        public WalletSendTransactionModel SendTransaction(SendTransactionRequest request)
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

        public StatusModel GetNodeStatus()
        {
            var model = new StatusModel
            {
                Version = this.fullNode.Version?.ToString() ?? "0",
                ProtocolVersion = (uint)(this.nodeSettings.ProtocolVersion),
                Agent = this.connectionManager.ConnectionSettings.Agent,
                ProcessId = Process.GetCurrentProcess().Id,
                Network = this.fullNode.Network.Name,
                ConsensusHeight = this.chainState.ConsensusTip?.Height,
                DataDirectoryPath = this.nodeSettings.DataDir,
                Testnet = this.network.IsTest(),
                RelayFee = this.nodeSettings.MinRelayTxFeeRate?.FeePerK?.ToUnit(MoneyUnit.BTC) ?? 0,
                RunningTime = this.dateTimeProvider.GetUtcNow() - this.fullNode.StartTime,
                CoinTicker = this.network.CoinTicker,
                State = this.fullNode.State.ToString()
            };


            var target = this.networkDifficulty.GetNetworkDifficulty();
            if (target != null)
                model.Difficulty = target.Difficulty;

            // Add the list of features that are enabled.
            foreach (IFullNodeFeature feature in this.fullNode.Services.Features)
            {
                model.FeaturesData.Add(new FeatureData
                {
                    Namespace = feature.GetType().ToString(),
                    State = feature.State
                });
            }

            // Include BlockStore Height if enabled
            if (this.chainState.BlockStoreTip != null)
                model.BlockStoreHeight = this.chainState.BlockStoreTip.Height;

            // Add the details of connected nodes.
            foreach (INetworkPeer peer in this.connectionManager.ConnectedPeers)
            {
                // var connectionManagerBehavior = peer.Behavior<IConnectionManagerBehavior>();

                var chainHeadersBehavior = peer.Behavior<ConsensusManagerBehavior>();
                var connectedPeer = new ConnectedPeerModel
                {
                    Version = peer.PeerVersion != null ? peer.PeerVersion.UserAgent : "[Unknown]",
                    RemoteSocketEndpoint = peer.RemoteSocketEndpoint.ToString(),
                    TipHeight = chainHeadersBehavior.BestReceivedTip != null ? chainHeadersBehavior.BestReceivedTip.Height : peer.PeerVersion?.StartHeight ?? -1,
                    IsInbound = peer.Inbound
                };

                if (connectedPeer.IsInbound)
                {
                    model.InboundPeers.Add(connectedPeer);
                }
                else
                {
                    model.OutboundPeers.Add(connectedPeer);
                }
            }

            return model;
        }


        public GetStakingInfoModel GetStakingInfo()
        {
            if (!this.fullNode.Network.Consensus.IsProofOfStake)
                throw new X1WalletException(HttpStatusCode.MethodNotAllowed, "Consensus is not Proof-of-Stake.");

            GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();
            return model;
        }
    }
}
