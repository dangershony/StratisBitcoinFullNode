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





        public async Task<string> SignMessageAsync(SignMessageRequest request)
        {
            using (var context = GetWalletContext())
            {
                return context.WalletManager.SignMessage(request.Password, request.WalletName,
                    request.ExternalAddress, request.Message);
            }
        }

        public void SetWalletName(string target)
        {
            if (this.walletName != null)
                throw new InvalidOperationException("walletName is already set - this controller must be a new instance per request!");
            this.walletName = target;
        }

        public async Task<bool> VerifyMessageAsync(VerifyRequest request)
        {
            using (var context = GetWalletContext())
            {
                return context.WalletManager.VerifySignedMessage(request.ExternalAddress, request.Message, request.Signature);
            }
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

        public async Task<WalletHistoryModel> GetHistoryAsync(WalletHistoryRequest request)
        {
            using (var context = GetWalletContext())
            {
                var model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                var histories = context.WalletManager.GetHistory();


                var transactionItems = new List<TransactionItemModel>();

                // Sorting the history items by descending dates. That includes received and sent dates.
                var items = histories.OrderBy(o => o.Transaction.IsConfirmed() ? 1 : 0)
                                                        .ThenByDescending(o => o.Transaction.SpendingDetails?.CreationTime ?? o.Transaction.CreationTime)
                                                        .ToList();

                // Represents a sublist containing only the transactions that have already been spent.
                var spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                var history = items.Where(t => !t.Address.IsChange || (t.Address.IsChange && t.Transaction.IsSpent())).ToList();

                // Represents a sublist of 'change' transactions.
                var allchange = items.Where(t => t.Address.IsChange).ToList();

                int itemsCount = 0;
                foreach (FlatAddressHistory item in history)
                {

                    if (itemsCount == MaxHistoryItemsPerAccount)
                    {
                        break;
                    }

                    TransactionData transaction = item.Transaction;
                    var address = item.Address;

                    // First we look for staking transaction as they require special attention.
                    // A staking transaction spends one of our inputs into 2 outputs or more, paid to the same address.
                    if (transaction.SpendingDetails?.IsCoinStake != null && transaction.SpendingDetails.IsCoinStake.Value)
                    {
                        // We look for the output(s) related to our spending input.
                        var relatedOutputs = items.Where(h => h.Transaction.Id == transaction.SpendingDetails.TransactionId && h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList();
                        if (relatedOutputs.Any())
                        {
                            // Add staking transaction details.
                            // The staked amount is calculated as the difference between the sum of the outputs and the input and should normally be equal to 1.
                            var stakingItem = new TransactionItemModel
                            {
                                Type = TransactionItemType.Staked,
                                ToAddress = address.Address,
                                Amount = relatedOutputs.Sum(o => o.Transaction.Amount) - transaction.Amount,
                                Id = transaction.SpendingDetails.TransactionId,
                                Timestamp = transaction.SpendingDetails.CreationTime,
                                ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                                BlockIndex = transaction.SpendingDetails.BlockIndex
                            };

                            transactionItems.Add(stakingItem);
                            itemsCount++;
                        }

                        // No need for further processing if the transaction itself is the output of a staking transaction.
                        if (transaction.IsCoinStake != null)
                        {
                            continue;
                        }
                    }

                    // If this is a normal transaction (not staking) that has been spent, add outgoing fund transaction details.
                    if (transaction.SpendingDetails != null && transaction.SpendingDetails.IsCoinStake == null)
                    {
                        // Create a record for a 'send' transaction.
                        uint256 spendingTransactionId = transaction.SpendingDetails.TransactionId;
                        var sentItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Send,
                            Id = spendingTransactionId,
                            Timestamp = transaction.SpendingDetails.CreationTime,
                            ConfirmedInBlock = transaction.SpendingDetails.BlockHeight,
                            BlockIndex = transaction.SpendingDetails.BlockIndex,
                            Amount = Money.Zero
                        };

                        // If this 'send' transaction has made some external payments, i.e the funds were not sent to another address in the wallet.
                        if (transaction.SpendingDetails.Payments != null)
                        {
                            sentItem.Payments = new List<PaymentDetailModel>();
                            foreach (PaymentDetails payment in transaction.SpendingDetails.Payments)
                            {
                                sentItem.Payments.Add(new PaymentDetailModel
                                {
                                    DestinationAddress = payment.DestinationAddress,
                                    Amount = payment.Amount
                                });

                                sentItem.Amount += payment.Amount;
                            }
                        }

                        // Get the change address for this spending transaction.
                        var changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

                        // Find all the spending details containing the spending transaction id and aggregate the sums.
                        // This is our best shot at finding the total value of inputs for this transaction.
                        var inputsAmount = new Money(spendingDetails.Where(t => t.Transaction.SpendingDetails.TransactionId == spendingTransactionId).Sum(t => t.Transaction.Amount));

                        // The fee is calculated as follows: funds in utxo - amount spent - amount sent as change.
                        sentItem.Fee = inputsAmount - sentItem.Amount - (changeAddress == null ? 0 : changeAddress.Transaction.Amount);

                        // Mined/staked coins add more coins to the total out.
                        // That makes the fee negative. If that's the case ignore the fee.
                        if (sentItem.Fee < 0)
                            sentItem.Fee = 0;

                        transactionItems.Add(sentItem);
                        itemsCount++;
                    }

                    // We don't show in history transactions that are outputs of staking transactions.
                    if (transaction.IsCoinStake != null && transaction.IsCoinStake.Value && transaction.SpendingDetails == null)
                    {
                        continue;
                    }

                    // Create a record for a 'receive' transaction.
                    if (transaction.IsCoinStake == null && !address.IsChange)
                    {
                        // Add incoming fund transaction details.
                        var receivedItem = new TransactionItemModel
                        {
                            Type = TransactionItemType.Received,
                            ToAddress = address.Address,
                            Amount = transaction.Amount,
                            Id = transaction.Id,
                            Timestamp = transaction.CreationTime,
                            ConfirmedInBlock = transaction.BlockHeight,
                            BlockIndex = transaction.BlockIndex
                        };

                        transactionItems.Add(receivedItem);
                        itemsCount++;
                    }
                }

                transactionItems = transactionItems.Distinct(new SentTransactionItemModelComparer()).Select(e => e).ToList();

                // Sort and filter the history items.
                List<TransactionItemModel> itemsToInclude = transactionItems.OrderByDescending(t => t.Timestamp)
                    .Where(x => string.IsNullOrEmpty(request.SearchQuery) || (x.Id.ToString() == request.SearchQuery || x.ToAddress == request.SearchQuery || x.Payments.Any(p => p.DestinationAddress == request.SearchQuery)))
                    .Skip(request.Skip ?? 0)
                    .Take(request.Take ?? transactionItems.Count)
                    .ToList();

                model.AccountsHistoryModel.Add(new AccountHistoryModel
                {
                    TransactionsHistory = itemsToInclude,
                    Name = "account 0",
                    CoinType = this.coinType,
                    HdPath = "no HdPath"
                });


                return model;
            }

        }


        public async Task<Balance> GetBalanceAsync(string walletName)
        {
            using (var context = GetWalletContext())
            {
                var balances = context.WalletManager.GetBalances();

                var walletBalance = new Balance { AmountConfirmed = Money.Zero, AmountUnconfirmed = Money.Zero, SpendableAmount = Money.Zero };

                foreach (var balance in balances)
                {
                    walletBalance.AmountConfirmed += balance.AmountConfirmed;
                    walletBalance.AmountUnconfirmed += balance.AmountUnconfirmed;
                    walletBalance.SpendableAmount += balance.SpendableAmount;
                }
                return walletBalance;

            }
        }


        public async Task<IActionResult> GetReceivedByAddressAsync(ReceivedByAddressRequest request)
        {
            using (var context = GetWalletContext())
            {
                AddressBalance balanceResult = context.WalletManager.GetAddressBalance(request.Address);
                return this.Json(new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed
                });
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
                recipients.Add(new Recipient
                {
                    ScriptPubKey = BitcoinAddress.Create(recipientModel.DestinationAddress, this.network).ScriptPubKey,
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
                var model = new KeyAddressesModel { Addresses = new List<KeyAddressModel>() };

                var unusedReceiveAddresses = context.WalletManager.GetUnusedAddresses(10, false);
                foreach (var addr in unusedReceiveAddresses)
                    model.Addresses.Add(new KeyAddressModel { Address = addr.Address, IsChange = addr.IsChange, IsUsed = false });
                return model;
            }
        }


        public async Task<IEnumerable<RemovedTransactionModel>> RemoveTransactionsAsync(RemoveTransactionsModel request)
        {
            using (var context = GetWalletContext())
            {
                var manager = context.WalletManager;
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = manager.RemoveAllTransactions(request.WalletName);
                }
                else if (request.FromDate != default(DateTime))
                {
                    result = manager.RemoveTransactionsFromDate(request.WalletName, request.FromDate);
                }
                else if (request.TransactionsIds != null)
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = manager.RemoveTransactionsByIds(ids);
                }
                else
                {
                    throw new WalletException("A filter specifying what transactions to remove must be set.");
                }

                // If the user chose to resync the wallet after removing transactions.
                if (result.Any() && request.ReSync)
                {
                    // From the list of removed transactions, check which one is the oldest and retrieve the block right before that time.
                    DateTimeOffset earliestDate = result.Min(r => r.creationTime);
                    ChainedHeader chainedHeader = this.chainIndexer.GetHeader(this.chainIndexer.GetHeightAtTime(earliestDate.DateTime));
                   
                    await this.walletManagerWrapper.WalletSyncManagerSyncFromHeightAsync(chainedHeader.Height - 1);
                }

                IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });

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
