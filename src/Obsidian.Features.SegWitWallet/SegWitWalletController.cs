using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
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

namespace Obsidian.Features.SegWitWallet
{
    public class SegWitWalletController : Controller
    {
        const int MaxHistoryItemsPerAccount = int.MaxValue;

       

        readonly WalletManagerFacade walletManagerFacade;
        readonly SegWitWalletTransactionHandler walletTransactionHandler;
        readonly IWalletSyncManager walletSyncManager;
        readonly CoinType coinType;
        readonly Network network;
        readonly IConnectionManager connectionManager;
        readonly ChainIndexer chainIndexer;
        readonly IBroadcasterManager broadcasterManager;
        readonly IDateTimeProvider dateTimeProvider;

        // needed for nodeStatus call
        readonly IFullNode fullNode;
        readonly NodeSettings nodeSettings;
        readonly IChainState chainState;
        readonly INetworkDifficulty networkDifficulty;

        readonly IPosMinting posMinting;

        public SegWitWalletManager GetManager(string walletName)
        {
            return this.walletManagerFacade.GetManager(walletName);
        }

        public SegWitWalletController(
            IWalletManager walletManagerFacade,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
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
            this.walletManagerFacade = (WalletManagerFacade)walletManagerFacade;
            this.walletTransactionHandler = (SegWitWalletTransactionHandler)walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
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


        public async Task<string> CreateAsync(WalletCreationRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                try
                {
                    Mnemonic requestMnemonic = string.IsNullOrEmpty(request.Mnemonic) ? null : new Mnemonic(request.Mnemonic);
                    return default(string);
                    throw new NotImplementedException();
                    //Mnemonic mnemonic = this.segWitWalletManager.CreateWallet(request.Password, request.Name, request.Passphrase, mnemonic: requestMnemonic);

                    //// start syncing the wallet from the creation date
                    //this.walletSyncManager.SyncFromDate(this.dateTimeProvider.GetUtcNow());

                    //return this.Json(mnemonic.ToString());
                }
                catch (WalletException e)
                {
                    throw new SegWitWalletException(HttpStatusCode.Conflict, "The wallet already exists.", e);

                }
                catch (NotSupportedException e)
                {
                    throw new SegWitWalletException(HttpStatusCode.BadGateway, "Could not create the wallet: " + e.Message, e);
                }
            });
        }



        public async Task<string> SignMessageAsync(SignMessageRequest request)
        {
            return await ExecuteAsync(request, async () => GetManager(request.WalletName).SignMessage(request.Password, request.WalletName,
                request.ExternalAddress, request.Message));
        }


        public async Task<bool> VerifyMessageAsync(VerifyRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                return GetManager(null/*request.WalletName*/).VerifySignedMessage(request.ExternalAddress, request.Message, request.Signature);
            });
        }


        public async Task LoadAsync(WalletLoadRequest request)
        {
            await ExecuteAsync(request, async () =>
            {
                this.walletManagerFacade.LoadWallet(null, request.Name);
            });
        }

        public void CreateNondeterministicWallet(string name, string keyEncryptionPassphrase)
        {
            //try
            //{
            //    if (this.wallets.ContainsKey(name))
            //        throw new InvalidOperationException($"A Wallet with name {name} is already loaded.");

            //    if (this.fileStorage.Exists($"{name}{WalletFileExtension}"))
            //        throw new InvalidOperationException(
            //            $"A Wallet with name {name} is already present in the data folder!");

            //    var wal = new KeyWallet
            //    {
            //        Name = name,
            //        CreationTime = DateTime.UtcNow,
            //        WalletType = nameof(KeyWallet),
            //        WalletTypeVersion = 1,
            //        Addresses = new List<KeyAddress>()
            //    };
            //    const int witnessVersion = 0;
            //    var bech32Prefix = "odx";  // https://github.com/bitcoin/bips/blob/master/bip-0173.mediawiki

            //    var uniqueIndex = 0;
            //    var adr = KeyAddress.CreateWithPrivateKey(StaticWallet.Key1Bytes, keyEncryptionPassphrase, KeyEncryption, this.network.Consensus.CoinType, uniqueIndex++, witnessVersion, bech32Prefix);
            //    wal.Addresses.Add(adr);
            //    var adr2 = KeyAddress.CreateWithPrivateKey(StaticWallet.Key2Bytes, keyEncryptionPassphrase, KeyEncryption, this.network.Consensus.CoinType, uniqueIndex++, witnessVersion, bech32Prefix);
            //    wal.Addresses.Add(adr2);



            //    UpdateKeysLookupLocked(wal.Addresses);
            //    // If the chain is downloaded, we set the height of the newly created Wallet to it.
            //    // However, if the chain is still downloading when the user creates a Wallet,
            //    // we wait until it is downloaded in order to set it. Otherwise, the height of the Wallet will be the height of the chain at that moment.
            //    if (this.chainIndexer.IsDownloaded())
            //    {
            //        this.UpdateLastBlockSyncedHeight(this.chainIndexer.Tip);
            //    }
            //    else
            //    {
            //        this.UpdateWhenChainDownloaded(this.dateTimeProvider.GetUtcNow());
            //    }

            //    // Save the changes to the file and add addresses to be tracked.
            //    this.SaveWallet();
            //}
            //catch (Exception e)
            //{
            //    this.logger.LogError($"Could not create Wallet: {e.Message}");
            //}

        }

        public async Task RecoverAsync(WalletRecoveryRequest request)
        {
            await ExecuteAsync(request, async () =>
            {
                try
                {
                    Wallet wallet = this.walletManagerFacade.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate, passphrase: request.Passphrase);

                    this.SyncFromBestHeightForRecoveredWallets(request.CreationDate);

                }
                catch (WalletException e)
                {
                    throw new SegWitWalletException(HttpStatusCode.Conflict, "Wallet already exists: " + e.Message, e);
                }
                catch (FileNotFoundException e)
                {
                    throw new SegWitWalletException(HttpStatusCode.NotFound, "Wallet not found.", e);
                }
            });
        }


        public async Task<WalletGeneralInfoModel> GetGeneralInfoAsync(WalletName request)
        {
            return await ExecuteAsync(request, async () =>
            {
                KeyWallet wallet = GetManager(request.Name).Wallet;

                var model = new WalletGeneralInfoModel
                {
                    Network = GetManager(request.Name).GetNetwork(),
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chainIndexer.Tip.Height,
                    IsChainSynced = this.chainIndexer.IsDownloaded(),
                    IsDecrypted = true
                };

                (string folder, IEnumerable<string> fileNameCollection) = this.walletManagerFacade.GetWalletsFiles();
                string searchFile = Path.ChangeExtension(request.Name, this.walletManagerFacade.GetWalletFileExtension());
                string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                if (folder != null && fileName != null)
                    model.WalletFilePath = Path.Combine(folder, fileName);
                return model;
            });
        }


        public async Task<WalletHistoryModel> GetHistoryAsync(WalletHistoryRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                FlatHistory[] histories = GetManager(request.WalletName).GetHistory();


                var transactionItems = new List<TransactionItemModel>();

                // Sorting the history items by descending dates. That includes received and sent dates.
                List<FlatHistory> items = histories.OrderBy(o => o.Transaction.IsConfirmed() ? 1 : 0)
                                                        .ThenByDescending(o => o.Transaction.SpendingDetails?.CreationTime ?? o.Transaction.CreationTime)
                                                        .ToList();

                // Represents a sublist containing only the transactions that have already been spent.
                List<FlatHistory> spendingDetails = items.Where(t => t.Transaction.SpendingDetails != null).ToList();

                // Represents a sublist of transactions associated with receive addresses + a sublist of already spent transactions associated with change addresses.
                // In effect, we filter out 'change' transactions that are not spent, as we don't want to show these in the history.
                List<FlatHistory> history = items.Where(t => !t.Address.IsChangeAddress() || (t.Address.IsChangeAddress() && t.Transaction.IsSpent())).ToList();

                // Represents a sublist of 'change' transactions.
                List<FlatHistory> allchange = items.Where(t => t.Address.IsChangeAddress()).ToList();

                int itemsCount = 0;
                foreach (FlatHistory item in history)
                {

                    if (itemsCount == MaxHistoryItemsPerAccount)
                    {
                        break;
                    }

                    TransactionData transaction = item.Transaction;
                    HdAddress address = item.Address;

                    // First we look for staking transaction as they require special attention.
                    // A staking transaction spends one of our inputs into 2 outputs or more, paid to the same address.
                    if (transaction.SpendingDetails?.IsCoinStake != null && transaction.SpendingDetails.IsCoinStake.Value)
                    {
                        // We look for the output(s) related to our spending input.
                        List<FlatHistory> relatedOutputs = items.Where(h => h.Transaction.Id == transaction.SpendingDetails.TransactionId && h.Transaction.IsCoinStake != null && h.Transaction.IsCoinStake.Value).ToList();
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
                        FlatHistory changeAddress = allchange.FirstOrDefault(a => a.Transaction.Id == spendingTransactionId);

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
                    if (transaction.IsCoinStake == null && !address.IsChangeAddress())
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
            });
        }


        public async Task<WalletBalance> GetBalanceAsync(WalletBalanceRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var balances = GetManager(request.WalletName).GetBalances(request.WalletName);

                var walletBalance = new WalletBalance { AmountConfirmed = Money.Zero, AmountUnconfirmed = Money.Zero, SpendableAmount = Money.Zero };

                foreach (var balance in balances)
                {
                    walletBalance.AmountConfirmed += balance.AmountConfirmed;
                    walletBalance.AmountUnconfirmed += balance.AmountUnconfirmed;
                    walletBalance.SpendableAmount += balance.SpendableAmount;
                }
                return walletBalance;
            });
        }


        public async Task<IActionResult> GetReceivedByAddressAsync(ReceivedByAddressRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                AddressBalance balanceResult = this.walletManagerFacade.GetAddressBalance(request.Address);
                return this.Json(new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed
                });
            });
        }


        public async Task<MaxSpendableAmountModel> GetMaximumSpendableBalanceAsync(WalletMaximumBalanceRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                (Money maximumSpendableAmount, Money Fee) transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(request.WalletName, request.AccountName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
                return new MaxSpendableAmountModel
                {
                    MaxSpendableAmount = transactionResult.maximumSpendableAmount,
                    Fee = transactionResult.Fee
                };
            });
        }


        public async Task<SpendableTransactionsModel> GetSpendableTransactionsAsync(SpendableTransactionsRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var spendableTransactions = GetManager(request.WalletName).GetSpendableTransactionsInAccount(request.WalletName, request.MinConfirmations);

                return new SpendableTransactionsModel
                {
                    SpendableTransactions = spendableTransactions.Select(st => new SpendableTransactionModel
                    {
                        Id = st.Transaction.Id,
                        Amount = st.Transaction.Amount,
                        Address = st.Address.Bech32,
                        Index = st.Transaction.Index,
                        IsChange = st.Address.IsChangeAddress(),
                        CreationTime = st.Transaction.CreationTime,
                        Confirmations = st.Confirmations
                    }).ToList()
                };
            });
        }


        public async Task<Money> GetTransactionFeeEstimateAsync(TxFeeEstimateRequest request)
        {
            return await ExecuteAsync(request, async () =>
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
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
                    FeeType = FeeParser.Parse(request.FeeType),
                    MinConfirmations = request.AllowUnconfirmed ? 0 : 1,
                    Recipients = recipients,
                    OpReturnData = request.OpReturnData,
                    OpReturnAmount = string.IsNullOrEmpty(request.OpReturnAmount) ? null : Money.Parse(request.OpReturnAmount),
                    Sign = false
                };
                Money model = this.walletTransactionHandler.EstimateFee(context);
                return model;
            });
        }


        public async Task<WalletBuildTransactionModel> BuildTransactionAsync(BuildTransactionRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                byte[] clientPublicKeyBytes = Convert.FromBase64String(request.ClientPublicKey);
                byte[] cipherV2Bytes = Convert.FromBase64String(request.Password);

                var passwordBytes = VCL.Decrypt(cipherV2Bytes, clientPublicKeyBytes, VCL.ECKeyPair.PrivateKey);
                var password = Encoding.UTF8.GetString(passwordBytes);
                request.Password = password;

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
                    AccountReference = new WalletAccountReference(request.WalletName, request.AccountName),
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
            });
        }


        public async Task<WalletSendTransactionModel> SendTransactionAsync(SendTransactionRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                if (!this.connectionManager.ConnectedPeers.Any())
                {
                    throw new SegWitWalletException(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", new Exception());
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
                    throw new SegWitWalletException(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, new Exception("Transaction Exception"));
                }

                return model;
            });
        }


        public async Task<WalletFileModel> ListWalletsFilesAsync()
        {
            return await ExecuteAsync("", async () =>
            {
                (string folderPath, IEnumerable<string> filesNames) result = this.walletManagerFacade.GetWalletsFiles();
                var model = new WalletFileModel
                {
                    WalletsPath = result.folderPath,
                    WalletsFiles = result.filesNames
                };
                return model;
            });
        }


        public async Task<IEnumerable<string>> ListAccountsAsync(ListAccountsModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                IEnumerable<HdAccount> result = this.walletManagerFacade.GetAccounts(request.WalletName);
                IEnumerable<string> model = result.Select(a => a.Name);
                return model;
            });
        }


        public async Task<string> GetUnusedAddressAsync(GetUnusedAddressModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var address = "";
                var result = GetManager(request.WalletName).GetUnusedAddress(request.WalletName);

                if (result != null)
                    address = result.Bech32;

                return address;
            });
        }


        public async Task<string[]> GetUnusedAddresses(GetUnusedAddressesModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                int count = int.Parse(request.Count);
                string[] addresses = GetManager(request.WalletName).GetUnusedAddresses(request.WalletName, count).Select(x => x.Bech32).ToArray();
                return addresses;
            });
        }


        public async Task<AddressesModel> GetAllAddressesAsync(GetAllAddressesModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var model = new AddressesModel
                {
                    Addresses = GetManager(request.WalletName).Wallet.Addresses.Select(address => new AddressModel
                    {
                        Address = address.Bech32,
                        IsUsed = address.Transactions.Any(),
                        IsChange = address.IsChangeAddress()
                    })
                };

                return model;
            });
        }


        public async Task<IEnumerable<RemovedTransactionModel>> RemoveTransactionsAsync(RemoveTransactionsModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = this.walletManagerFacade.RemoveAllTransactions(request.WalletName);
                }
                else if (request.FromDate != default(DateTime))
                {
                    result = this.walletManagerFacade.RemoveTransactionsFromDate(request.WalletName, request.FromDate);
                }
                else if (request.TransactionsIds != null)
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = this.walletManagerFacade.RemoveTransactionsByIds(request.WalletName, ids);
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

                    // Update the wallet and save it to the file system.
                    Wallet wallet = this.walletManagerFacade.GetWallet(request.WalletName);
                    wallet.SetLastBlockDetails(chainedHeader);
                    this.walletManagerFacade.SaveWallet(wallet);

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1);
                }

                IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });

                return model;
            });
        }


        public async Task SyncAsync(HashModel request)
        {
            await ExecuteAsync(request, async () =>
            {
                ChainedHeader block = this.chainIndexer.GetHeader(uint256.Parse(request.Hash));

                if (block == null)
                {
                    throw new SegWitWalletException(HttpStatusCode.BadRequest, $"Block with hash {request.Hash} was not found on the blockchain.", new Exception());
                }

                this.walletSyncManager.SyncFromHeight(block.Height);
            });
        }


        public async Task SyncFromDate(WalletSyncFromDateRequest request)
        {
            await ExecuteAsync(request, async () =>
            {
                this.walletSyncManager.SyncFromDate(request.Date);
            });
        }

        public async Task<WalletSendTransactionModel> SplitCoinsAsync(SplitCoinsRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                KeyAddress address = GetManager(request.WalletName).GetUnusedAddress(request.WalletName);

                Money totalAmount = request.TotalAmountToSplit;
                Money singleUtxoAmount = totalAmount / request.UtxosCount;

                var recipients = new List<Recipient>(request.UtxosCount);
                for (int i = 0; i < request.UtxosCount; i++)
                    recipients.Add(new Recipient { ScriptPubKey = address.GetPaymentScript(), Amount = singleUtxoAmount });

                var context = new TransactionBuildContext(this.network)
                {
                    AccountReference = new WalletAccountReference { WalletName = request.WalletName, AccountName = request.AccountName },
                    MinConfirmations = 1,
                    Shuffle = true,
                    WalletPassword = request.WalletPassword,
                    Recipients = recipients,
                    Time = (uint)this.dateTimeProvider.GetAdjustedTimeAsUnixTimestamp()
                };

                Transaction transactionResult = this.walletTransactionHandler.BuildTransaction(context);

                WalletSendTransactionModel model = await SendTransactionAsync(new SendTransactionRequest(transactionResult.ToHex()));
                return model;
            });
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
                throw new SegWitWalletException(HttpStatusCode.MethodNotAllowed, "Consensus is not Proof-of-Stake.", null);

            GetStakingInfoModel model = this.posMinting != null ? this.posMinting.GetGetStakingInfoModel() : new GetStakingInfoModel();
            return model;
        }

        void SyncFromBestHeightForRecoveredWallets(DateTime walletCreationDate)
        {
            // After recovery the wallet needs to be synced.
            // We only sync if the syncing process needs to go back.
            int blockHeightToSyncFrom = this.chainIndexer.GetHeightAtTime(walletCreationDate);
            int currentSyncingHeight = this.walletSyncManager.WalletTip.Height;

            if (blockHeightToSyncFrom < currentSyncingHeight)
            {
                this.walletSyncManager.SyncFromHeight(blockHeightToSyncFrom);
            }
        }

        async Task<TResult> ExecuteAsync<T, TResult>(T request, Func<Task<TResult>> func) where T : class
        {
            try
            {

                await SegWitWalletManager.WalletSemaphore.WaitAsync();
                return await func();
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }

        async Task ExecuteAsync<T>(T request, Func<Task> func) where T : class
        {
            try
            {

                await SegWitWalletManager.WalletSemaphore.WaitAsync();
                await func();
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }


    }
}
