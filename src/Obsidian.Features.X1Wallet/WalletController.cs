using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Builder.Feature;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Miner.Staking;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.P2P.Peer;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    public class WalletController : Controller
    {
        const int MaxHistoryItemsPerAccount = 500;

        readonly WalletManagerWrapper walletManagerWrapper;
        readonly TransactionHandler walletTransactionHandler;
        readonly CoinType coinType;
        readonly Network network;
        readonly IConnectionManager connectionManager;
        readonly ChainIndexer chainIndexer;
        readonly IBroadcasterManager broadcasterManager;
        readonly IDateTimeProvider dateTimeProvider;

        readonly IFullNode fullNode;
        readonly NodeSettings nodeSettings;
        readonly IChainState chainState;
        readonly INetworkDifficulty networkDifficulty;

        readonly IPosMinting posMinting;

        string walletName;


        WalletContext GetWalletContext()
        {
            return this.walletManagerWrapper.GetWalletContext(this.walletName);
        }

        public WalletController(
            WalletManagerWrapper walletManagerFacade,
            IWalletTransactionHandler walletTransactionHandler,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider,
            IFullNode fullNode,
            NodeSettings nodeSettings,
            IChainState chainState,
            INetworkDifficulty networkDifficulty,
            IPosMinting posMinting
            )
        {
            this.walletManagerWrapper = (WalletManagerWrapper)walletManagerFacade;
            this.walletTransactionHandler = (TransactionHandler)walletTransactionHandler;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chainIndexer = chainIndexer;
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
            this.fullNode = fullNode;
            this.nodeSettings = nodeSettings;
            this.chainState = chainState;
            this.networkDifficulty = networkDifficulty;

            this.posMinting = posMinting;
        }







        public void SetWalletName(string target)
        {
            if (this.walletName != null)
                throw new InvalidOperationException("walletName is already set - this controller must be a new instance per request!");
            this.walletName = target;
        }



        public async Task<LoadWalletResponse> LoadAsync()
        {
            using (var context = GetWalletContext())
            {
                ; // this will load the wallet or ensure it's loaded, because ExecuteAsync does that.
                return context.WalletManager.LoadWallet();
            }
        }

        public async Task CreateKeyWalletAsync(WalletCreateRequest walletCreateRequest)
        {
            await this.walletManagerWrapper.CreateKeyWalletAsync(walletCreateRequest);
        }

        public async Task<ImportKeysResponse> ImportKeysAsync(ImportKeysRequest importKeysRequest)
        {
            using (var context = GetWalletContext())
            {
                return await context.WalletManager.ImportKeysAsync(importKeysRequest);
            }

        }

        public async Task<ExportKeysResponse> ExportKeysAsync(ExportKeysRequest importKeysRequest)
        {
            using (var context = GetWalletContext())
            {
                return await context.WalletManager.ExportKeysAsync(importKeysRequest);
            }

        }

        public async Task<WalletGeneralInfoModel> GetGeneralInfoAsync()
        {
            using (var context = GetWalletContext())
            {
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
        }

        public async Task StartStaking(StartStakingRequest startStakingRequest)
        {
            using (var context = GetWalletContext())
            {
                context.WalletManager.StartStaking(startStakingRequest.Password);
            }
        }

        public async Task StopStaking()
        {
            using (var context = GetWalletContext())
            {
                context.WalletManager.StopStaking();
            }
        }




        public async Task<Balance> GetBalanceAsync(string walletName)
        {
            using (var context = GetWalletContext())
            {
                return context.WalletManager.GetConfirmedWalletBalance();
            }
        }





        public async Task<MaxSpendableAmountModel> GetMaximumSpendableBalanceAsync(WalletMaximumBalanceRequest request)
        {
            (Money maximumSpendableAmount, Money Fee) transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(this.walletName, this.walletName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
            return new MaxSpendableAmountModel
            {
                MaxSpendableAmount = transactionResult.maximumSpendableAmount,
                Fee = transactionResult.Fee
            };
        }


        public async Task<SpendableTransactionsModel> GetSpendableTransactionsAsync(SpendableTransactionsRequest request)
        {
            using (var context = GetWalletContext())
            {
                var spendableTransactions = context.WalletManager.GetAllSpendableTransactions(request.MinConfirmations);

                return new SpendableTransactionsModel
                {
                    SpendableTransactions = spendableTransactions.Select(st => new SpendableTransactionModel
                    {
                        Id = st.Transaction.Id,
                        Amount = st.Transaction.Amount,
                        Address = st.Address.Address,
                        Index = st.Transaction.Index,
                        IsChange = st.Address.IsChange,
                        CreationTime = st.Transaction.CreationTime,
                        Confirmations = st.Confirmations
                    }).ToList()
                };
            }

        }


        public async Task<Money> GetTransactionFeeEstimateAsync(TxFeeEstimateRequest request)
        {
            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                var address = P2WpkhAddress.FromString(recipientModel.DestinationAddress, this.network);
                if (address == null)
                    throw new NotSupportedException($"Only {nameof(P2WpkhAddress)}es are supported at this time.");
                recipients.Add(new Recipient
                {
                    ScriptPubKey = address.GetScriptPubKey(),
                    Amount = recipientModel.Amount
                });
            }

            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(this.walletName, this.walletName),
                FeeType = FeeParser.Parse(request.FeeType),
                MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                Recipients = recipients,
                OpReturnData = request.OpReturnData,
                OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                Sign = false
            };
            Money model = this.walletTransactionHandler.EstimateFee(context);
            return model;
        }


        public async Task<WalletBuildTransactionModel> BuildTransactionAsync(BuildTransactionRequest request)
        {
            var recipients = new List<Recipient>();
            foreach (RecipientModel recipientModel in request.Recipients)
            {
                recipients.Add(new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
                    Amount = recipientModel.Amount
                });
            }

            var context = new TransactionBuildContext(this.network)
            {
                AccountReference = new WalletAccountReference(this.walletName, this.walletName),
                TransactionFee = string.IsNullOrEmpty(request.FeeAmount) ? null : Money.Parse(request.FeeAmount),
                MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                Shuffle = request.ShuffleOutputs ?? true, // We shuffle transaction outputs by default as it's better for anonymity.
                OpReturnData = request.OpReturnData,
                OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                WalletPassword = request.Password,
                SelectedInputs = request.Outpoints?.Select(u => new OutPoint(uint256.Parse(u.TransactionId), u.Index)).ToList(),
                AllowOtherInputs = false,
                Recipients = recipients
            };

            if (!string.IsNullOrEmpty(request.FeeType))
            {
                context.FeeType = FeeParser.Parse(request.FeeType);
            }

            Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

            var model = new WalletBuildTransactionModel
            {
                Hex = transactionResult.ToHex(),
                Fee = context.TransactionFee,
                TransactionId = transactionResult.GetHash()
            };

            return model;
        }


        public async Task<WalletSendTransactionModel> SendTransactionAsync(SendTransactionRequest request)
        {
            using (var context = GetWalletContext())
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

        }


        public async Task<WalletFileModel> ListWalletsFilesAsync()
        {
            (string folderPath, IEnumerable<string> filesNames) result = this.walletManagerWrapper.GetWalletsFiles();
            var model = new WalletFileModel
            {
                WalletsPath = result.folderPath,
                WalletsFiles = result.filesNames
            };
            return model;
        }


        public async Task<KeyAddressesModel> GetUnusedReceiveAddresses()
        {
            using (var context = GetWalletContext())
            {
                var unusedAddress = context.WalletManager.GetUnusedAddress();
                if (unusedAddress == null)
                    throw new X1WalletException(HttpStatusCode.BadRequest,
                        "The wallet doesn't have any unused addresses left.", null);

                var model = new KeyAddressesModel { Addresses = new List<KeyAddressModel>() };
                model.Addresses.Add(new KeyAddressModel { Address = unusedAddress.Address, IsChange = false, IsUsed = false, FullAddress = unusedAddress });
                return model;
            }
        }





        public async Task SyncAsync(HashModel request)
        {
            ChainedHeader block = this.chainIndexer.GetHeader(uint256.Parse(request.Hash));

            if (block == null)
            {
                throw new X1WalletException(HttpStatusCode.BadRequest, $"Block with hash {request.Hash} was not found on the blockchain.", new Exception());
            }

            await this.walletManagerWrapper.WalletSyncManagerSyncFromHeightAsync(block.Height);
        }


        public async Task SyncFromDate(WalletSyncFromDateRequest request)
        {
            await this.walletManagerWrapper.WalletSyncManagerSyncFromDateAsync(request.Date);
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
                var connectionManagerBehavior = peer.Behavior<IConnectionManagerBehavior>();
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
                throw new X1WalletException(HttpStatusCode.MethodNotAllowed, "Consensus is not Proof-of-Stake.", null);

            GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();
            return model;
        }
    }
}
