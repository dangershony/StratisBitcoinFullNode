using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Bitcoin.Utilities.ModelStateErrors;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.SegWitWallet.Controllers
{
    /// <summary>
    /// Controller providing operations on a wallet.
    /// </summary>
    [Route("api/[controller]")]
    public class SegWitWalletController : Controller
    {
        const int MaxHistoryItemsPerAccount = 500;

        readonly SegWitWalletManager segWitWalletManager;
        readonly SegWitWalletTransactionHandler walletTransactionHandler;
        readonly IWalletSyncManager walletSyncManager;
        readonly CoinType coinType;
        readonly Network network;
        readonly IConnectionManager connectionManager;
        readonly ChainIndexer chainIndexer;
        readonly ILogger logger;
        readonly IBroadcasterManager broadcasterManager;
        readonly IDateTimeProvider dateTimeProvider;

        public SegWitWalletController(
            ILoggerFactory loggerFactory,
            SegWitWalletManager segWitWalletManager,
            IWalletTransactionHandler walletTransactionHandler,
            IWalletSyncManager walletSyncManager,
            IConnectionManager connectionManager,
            Network network,
            ChainIndexer chainIndexer,
            IBroadcasterManager broadcasterManager,
            IDateTimeProvider dateTimeProvider)
        {
            this.segWitWalletManager = segWitWalletManager;
            this.walletTransactionHandler = (SegWitWalletTransactionHandler)walletTransactionHandler;
            this.walletSyncManager = walletSyncManager;
            this.connectionManager = connectionManager;
            this.network = network;
            this.coinType = (CoinType)network.Consensus.CoinType;
            this.chainIndexer = chainIndexer;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.broadcasterManager = broadcasterManager;
            this.dateTimeProvider = dateTimeProvider;
        }

        /// <summary>
        /// Creates a new wallet on this full node.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to create a wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody]WalletCreationRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                try
                {
                    Mnemonic requestMnemonic = string.IsNullOrEmpty(request.Mnemonic) ? null : new Mnemonic(request.Mnemonic);
                    throw new NotImplementedException();
                    //Mnemonic mnemonic = this.segWitWalletManager.CreateWallet(request.Password, request.Name, request.Passphrase, mnemonic: requestMnemonic);

                    //// start syncing the wallet from the creation date
                    //this.walletSyncManager.SyncFromDate(this.dateTimeProvider.GetUtcNow());

                    //return this.Json(mnemonic.ToString());
                }
                catch (WalletException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.Conflict, e.Message); // indicates that this wallet already exists
                }
                catch (NotSupportedException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.BadGateway, "There was a problem creating a wallet: " + e.Message);
                }
            });
        }

        /// <summary>
        /// Signs a message and returns the signature.
        /// </summary>
        /// <param name="request">The object containing the parameters used to sign a message.</param>
        /// <returns>A JSON object containing the generated signature.</returns>
        [Route("signmessage")]
        [HttpPost]
        public async Task<IActionResult> SignMessageAsync([FromBody]SignMessageRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                string signature = this.segWitWalletManager.SignMessage(request.Password, request.WalletName, request.ExternalAddress, request.Message);
                return this.Json(signature);
            });
        }

        /// <summary>
        /// Verifies the signature of a message.
        /// </summary>
        /// <param name="request">The object containing the parameters verify a signature.</param>
        /// <returns>A JSON object containing the result of the verification.</returns>
        [Route("verifymessage")]
        [HttpPost]
        public async Task<IActionResult> VerifyMessageAsync([FromBody]VerifyRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                bool result =
                    this.segWitWalletManager.VerifySignedMessage(request.ExternalAddress, request.Message, request.Signature);
                return this.Json(result.ToString());
            });
        }

        /// <summary>
        /// Loads a previously created wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to load an existing wallet</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [Route("load")]
        [HttpPost]
        public async Task<IActionResult> LoadAsync([FromBody]WalletLoadRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                try
                {
                    this.segWitWalletManager.LoadWallet(request.Password, request.Name);
                    return this.Ok();
                }
                catch (FileNotFoundException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.NotFound, "This wallet was not found at the specified location.");
                }
                catch (SecurityException e)
                {
                    // indicates that the password is wrong
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.Forbidden, "Wrong password, please try again.");
                }
                catch (Exception e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.BadRequest, e.Message);
                }
            });
        }

        /// <summary>
        /// Recovers an existing wallet.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet.</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover")]
        [HttpPost]
        public async Task<IActionResult> RecoverAsync([FromBody]WalletRecoveryRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                try
                {
                    Wallet wallet = this.segWitWalletManager.RecoverWallet(request.Password, request.Name, request.Mnemonic, request.CreationDate, passphrase: request.Passphrase);

                    this.SyncFromBestHeightForRecoveredWallets(request.CreationDate);

                    return this.Ok();
                }
                catch (WalletException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.Conflict, "Wallet already exists: " + e.Message);
                }
                catch (FileNotFoundException e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.NotFound, "Wallet not found.");
                }
                catch (Exception e)
                {
                    this.logger.LogError("Exception occurred: {0}", e.ToString());
                    return BuildErrorResponse(HttpStatusCode.BadRequest, e.Message);
                }
            });
        }

        /// <summary>
        /// Gets some general information about a wallet. This includes the network the wallet is for,
        /// the creation date and time for the wallet, the height of the blocks the wallet currently holds,
        /// and the number of connected nodes. 
        /// </summary>
        /// <param name="request">The name of the wallet to get the information for.</param>
        /// <returns>A JSON object containing the wallet information.</returns>
        [Route("general-info")]
        [HttpGet]
        public async Task<IActionResult> GetGeneralInfoAsync([FromQuery] WalletName request)
        {
            return await ExecuteAsync(request, async () =>
            {

                KeyWallet wallet = this.segWitWalletManager.GetSegWitWallet(request.Name);

                var model = new WalletGeneralInfoModel
                {
                    Network = this.segWitWalletManager.GetNetwork(),
                    CreationTime = wallet.CreationTime,
                    LastBlockSyncedHeight = wallet.LastBlockSyncedHeight,
                    ConnectedNodes = this.connectionManager.ConnectedPeers.Count(),
                    ChainTip = this.chainIndexer.Tip.Height,
                    IsChainSynced = this.chainIndexer.IsDownloaded(),
                    IsDecrypted = true
                };

                (string folder, IEnumerable<string> fileNameCollection) = this.segWitWalletManager.GetWalletsFiles();
                string searchFile = Path.ChangeExtension(request.Name, this.segWitWalletManager.GetWalletFileExtension());
                string fileName = fileNameCollection.FirstOrDefault(i => i.Equals(searchFile));
                if (folder != null && fileName != null)
                    model.WalletFilePath = Path.Combine(folder, fileName);

                return Json(model);
            });
        }

        /// <summary>
        /// Gets the history of a wallet. This includes the transactions held by the entire wallet
        /// or a single account if one is specified. 
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's history.</param>
        /// <returns>A JSON object containing the wallet history.</returns>
        [Route("history")]
        [HttpGet]
        public async Task<IActionResult> GetHistoryAsync([FromQuery] WalletHistoryRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var model = new WalletHistoryModel();

                // Get a list of all the transactions found in an account (or in a wallet if no account is specified), with the addresses associated with them.
                IEnumerable<AccountHistory> accountsHistory = this.segWitWalletManager.GetHistory(request.WalletName, request.AccountName);

                foreach (AccountHistory accountHistory in accountsHistory)
                {
                    var transactionItems = new List<TransactionItemModel>();

                    // Sorting the history items by descending dates. That includes received and sent dates.
                    List<FlatHistory> items = accountHistory.History
                                                            .OrderBy(o => o.Transaction.IsConfirmed() ? 1 : 0)
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
                        Name = accountHistory.Account.Name,
                        CoinType = this.coinType,
                        HdPath = accountHistory.Account.HdPath
                    });
                }

                return this.Json(model);
            });
        }

        /// <summary>
        /// Gets the balance of a wallet in STRAT (or sidechain coin). Both the confirmed and unconfirmed balance are returned.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's balance.</param>
        /// <returns>A JSON object containing the wallet balance.</returns>
        [Route("balance")]
        [HttpGet]
        public async Task<IActionResult> GetBalanceAsync([FromQuery] WalletBalanceRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var model = new WalletBalanceModel();

                IEnumerable<AccountBalance> balances = this.segWitWalletManager.GetBalances(request.WalletName, request.AccountName);

                foreach (AccountBalance balance in balances)
                {
                    HdAccount account = balance.Account;
                    model.AccountsBalances.Add(new AccountBalanceModel
                    {
                        CoinType = this.coinType,
                        Name = account.Name,
                        HdPath = account.HdPath,
                        AmountConfirmed = balance.AmountConfirmed,
                        AmountUnconfirmed = balance.AmountUnconfirmed,
                        SpendableAmount = balance.SpendableAmount
                    });
                }

                return this.Json(model);
            });
        }

        /// <summary>
        /// Gets the balance at a specific wallet address in STRAT (or sidechain coin).
        /// Both the confirmed and unconfirmed balance are returned.
        /// This method gets the UTXOs at the address which the wallet can spend.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the balance
        /// at a specific wallet address.</param>
        /// <returns>A JSON object containing the balance, fee, and an address for the balance.</returns>
        [Route("received-by-address")]
        [HttpGet]
        public async Task<IActionResult> GetReceivedByAddressAsync([FromQuery] ReceivedByAddressRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                AddressBalance balanceResult = this.segWitWalletManager.GetAddressBalance(request.Address);
                return this.Json(new AddressBalanceModel
                {
                    CoinType = this.coinType,
                    Address = balanceResult.Address,
                    AmountConfirmed = balanceResult.AmountConfirmed,
                    AmountUnconfirmed = balanceResult.AmountUnconfirmed
                });
            });
        }

        /// <summary>
        /// Gets the maximum spendable balance for an account along with the fee required to spend it.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the
        /// maximum spendable balance on an account.</param>
        /// <returns>A JSON object containing the maximum spendable balance for an account
        /// along with the fee required to spend it.</returns>
        [Route("maxbalance")]
        [HttpGet]
        public async Task<IActionResult> GetMaximumSpendableBalanceAsync([FromQuery] WalletMaximumBalanceRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                (Money maximumSpendableAmount, Money Fee) transactionResult = this.walletTransactionHandler.GetMaximumSpendableAmount(new WalletAccountReference(request.WalletName, request.AccountName), FeeParser.Parse(request.FeeType), request.AllowUnconfirmed);
                return Json(new MaxSpendableAmountModel
                {
                    MaxSpendableAmount = transactionResult.maximumSpendableAmount,
                    Fee = transactionResult.Fee
                });
            });
        }

        /// <summary>
        /// Gets the spendable transactions for an account with the option to specify how many confirmations
        /// a transaction needs to be included.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve the spendable 
        /// transactions for an account.</param>
        /// <returns>A JSON object containing the spendable transactions for an account.</returns>
        [Route("spendable-transactions")]
        [HttpGet]
        public async Task<IActionResult> GetSpendableTransactionsAsync([FromQuery] SpendableTransactionsRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var spendableTransactions = this.segWitWalletManager.GetSpendableTransactionsInAccount(request.WalletName, request.MinConfirmations);

                return Json(new SpendableTransactionsModel
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
                });
            });
        }

        /// <summary>
        /// Gets a fee estimate for a specific transaction.
        /// Fee can be estimated by creating a <see cref="TransactionBuildContext"/> with no password
        /// and then building the transaction and retrieving the fee from the context.
        /// </summary>
        /// <param name="request">An object containing the parameters used to estimate the fee 
        /// for a specific transaction.</param>
        /// <returns>The estimated fee for the transaction.</returns>
        [Route("estimate-txfee")]
        [HttpPost]
        public async Task<IActionResult> GetTransactionFeeEstimateAsync([FromBody]TxFeeEstimateRequest request)
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

                return Json(this.walletTransactionHandler.EstimateFee(context));
            });
        }

        /// <summary>
        /// Builds a transaction and returns the hex to use when executing the transaction.
        /// </summary>
        /// <param name="request">An object containing the parameters used to build a transaction.</param>
        /// <returns>A JSON object including the transaction ID, the hex used to execute
        /// the transaction, and the transaction fee.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public async Task<IActionResult> BuildTransactionAsync([FromBody] BuildTransactionRequest request)
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

                return this.Json(model);
            });
        }

        /// <summary>
        /// Sends a transaction that has already been built.
        /// Use the /api/Wallet/build-transaction call to create transactions.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters used to a send transaction request.</param>
        /// <returns>A JSON object containing information about the sent transaction.</returns>
        [Route("send-transaction")]
        [HttpPost]
        public async Task<IActionResult> SendTransactionAsync([FromBody] SendTransactionRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                if (!this.connectionManager.ConnectedPeers.Any())
                {
                    this.logger.LogTrace("(-)[NO_CONNECTED_PEERS]");
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.Forbidden, "Can't send transaction: sending transaction requires at least one connection!", string.Empty);
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
                    this.logger.LogError("Exception occurred: {0}", transactionBroadCastEntry.ErrorMessage);
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, "Transaction Exception");
                }

                return this.Json(model);
            });
        }

        /// <summary>
        /// Lists all the files found in the default wallet folder.
        /// </summary>
        /// <returns>A JSON object containing the wallet folder path and
        /// the names of the files found within the folder.</returns>
        [Route("files")]
        [HttpGet]
        public async Task<IActionResult> ListWalletsFilesAsync()
        {
            return await ExecuteAsync("", async () =>
            {
                (string folderPath, IEnumerable<string> filesNames) result = this.segWitWalletManager.GetWalletsFiles();
                var model = new WalletFileModel
                {
                    WalletsPath = result.folderPath,
                    WalletsFiles = result.filesNames
                };
                return this.Json(model);
            });
        }

        /// <summary>
        /// Gets a list of accounts for the specified wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to list the accounts for a wallet.</param>
        /// <returns>A JSON object containing a list of accounts for the specified wallet.</returns>
        [Route("accounts")]
        [HttpGet]
        public async Task<IActionResult> ListAccountsAsync([FromQuery]ListAccountsModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                IEnumerable<HdAccount> result = this.segWitWalletManager.GetAccounts(request.WalletName);
                return this.Json(result.Select(a => a.Name));
            });
        }

        /// <summary>
        /// Gets an unused address (in the Base58 format) for a wallet account. This address will not have been assigned
        /// to any known UTXO (neither to pay funds into the wallet or to pay change back to the wallet).
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to retrieve an
        /// unused address for a wallet account.</param>
        /// <returns>A JSON object containing the last created and unused address (in Base58 format).</returns>
        [Route("unusedaddress")]
        [HttpGet]
        public async Task<IActionResult> GetUnusedAddressAsync([FromQuery]GetUnusedAddressModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                var address = "";
                var result = this.segWitWalletManager.GetUnusedAddress(request.WalletName);

                if (result != null)
                    address = result.Bech32;

                return this.Json(address);
            });
        }

        /// <summary>
        /// Gets a specified number of unused addresses (in the Base58 format) for a wallet account. These addresses
        /// will not have been assigned to any known UTXO (neither to pay funds into the wallet or to pay change back
        /// to the wallet).
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// unused addresses for a wallet account.</param>
        /// <returns>A JSON object containing the required amount of unused addresses (in Base58 format).</returns>
        /// </summary>
        [Route("unusedaddresses")]
        [HttpGet]
        public async Task<IActionResult> GetUnusedAddresses([FromQuery]GetUnusedAddressesModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                int count = int.Parse(request.Count);
                var addresses = this.segWitWalletManager.GetUnusedAddresses(request.WalletName, count).Select(x => x.Bech32).ToArray();
                return Json(addresses);
            });
        }

        /// <summary>
        /// Gets all addresses for a wallet account.
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// all addresses for a wallet account.</param>
        /// <returns>A JSON object containing all addresses for a wallet account (in Base58 format).</returns>
        /// </summary>
        [Route("addresses")]
        [HttpGet]
        public async Task<IActionResult> GetAllAddressesAsync([FromQuery]GetAllAddressesModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                KeyWallet wallet = this.segWitWalletManager.GetSegWitWallet(request.WalletName);
                if (wallet == null)
                    throw new WalletException($"No wallet with the name '{request.WalletName}' could be found.");

                var model = new AddressesModel
                {
                    Addresses = wallet.Addresses.Select(address => new AddressModel
                    {
                        Address = address.Bech32,
                        IsUsed = address.Transactions.Any(),
                        IsChange = address.IsChangeAddress()
                    })
                };

                return Json(model);
            });
        }

        /// <summary>
        /// Removes transactions from the wallet.
        /// You might want to remove transactions from a wallet if some unconfirmed transactions disappear
        /// from the blockchain or the transaction fields within the wallet are updated and a refresh is required to
        /// populate the new fields. 
        /// In one situation, you might notice several unconfirmed transaction in the wallet, which you now know were
        /// never confirmed. You can use this API to correct this by specifying a date and time before the first
        /// unconfirmed transaction thereby removing all transactions after this point. You can also request a resync as
        /// part of the call, which calculates the block height for the earliest removal. The wallet sync manager then
        /// proceeds to resync from there reinstating the confirmed transactions in the wallet. You can also cherry pick
        /// transactions to remove by specifying their transaction ID. 
        /// 
        /// <param name="request">An object containing the necessary parameters to remove transactions
        /// from a wallet. The includes several options for specifying the transactions to remove.</param>
        /// <returns>A JSON object containing all removed transactions identified by their
        /// transaction ID and creation time.</returns>
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [Route("remove-transactions")]
        [HttpDelete]
        public async Task<IActionResult> RemoveTransactionsAsync([FromQuery]RemoveTransactionsModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                HashSet<(uint256 transactionId, DateTimeOffset creationTime)> result;

                if (request.DeleteAll)
                {
                    result = this.segWitWalletManager.RemoveAllTransactions(request.WalletName);
                }
                else if (request.FromDate != default(DateTime))
                {
                    result = this.segWitWalletManager.RemoveTransactionsFromDate(request.WalletName, request.FromDate);
                }
                else if (request.TransactionsIds != null)
                {
                    IEnumerable<uint256> ids = request.TransactionsIds.Select(uint256.Parse);
                    result = this.segWitWalletManager.RemoveTransactionsByIds(request.WalletName, ids);
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
                    Wallet wallet = this.segWitWalletManager.GetWallet(request.WalletName);
                    wallet.SetLastBlockDetails(chainedHeader);
                    this.segWitWalletManager.SaveWallet(wallet);

                    // Start the syncing process from the block before the earliest transaction was seen.
                    this.walletSyncManager.SyncFromHeight(chainedHeader.Height - 1);
                }

                IEnumerable<RemovedTransactionModel> model = result.Select(r => new RemovedTransactionModel
                {
                    TransactionId = r.transactionId,
                    CreationTime = r.creationTime
                });

                return Json(model);
            });
        }

        /// <summary>
        /// Requests the node resyncs from a block specified by its block hash.
        /// Internally, the specified block is taken as the new wallet tip
        /// and all blocks after it are resynced.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters
        /// to request a resync.</param>
        /// <returns>A value of Ok if the resync was successful.</returns>
        [HttpPost]
        [Route("sync")]
        public async Task<IActionResult> SyncAsync([FromBody] HashModel request)
        {
            return await ExecuteAsync(request, async () =>
            {
                ChainedHeader block = this.chainIndexer.GetHeader(uint256.Parse(request.Hash));

                if (block == null)
                {
                    return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, $"Block with hash {request.Hash} was not found on the blockchain.", string.Empty);
                }

                this.walletSyncManager.SyncFromHeight(block.Height);
                return this.Ok();
            });
        }

        /// <summary>
        /// Request the node resyncs starting from a given date and time.
        /// Internally, the first block created on or after the supplied date and time
        /// is taken as the new wallet tip and all blocks after it are resynced.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters
        /// to request a resync.</param>
        /// <returns>A value of Ok if the resync was successful.</returns>
        [HttpPost]
        [Route("sync-from-date")]
        public async Task<IActionResult> SyncFromDate([FromBody] WalletSyncFromDateRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                this.walletSyncManager.SyncFromDate(request.Date);

                return this.Ok();
            });
        }

        /// <summary>
        /// Creates requested amount of UTXOs each of equal value.
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [HttpPost]
        [Route("splitcoins")]
        public async Task<IActionResult> SplitCoinsAsync([FromBody] SplitCoinsRequest request)
        {
            return await ExecuteAsync(request, async () =>
            {
                KeyAddress address = this.segWitWalletManager.GetUnusedAddress(request.WalletName);

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

                return await SendTransactionAsync(new SendTransactionRequest(transactionResult.ToHex()));
            });
        }

        /// <summary>
        /// Provides the server's public key to the client.
        /// </summary>
        /// <returns>Server public key.</returns>
        [HttpGet]
        [Route("getpublickey")]
        public async Task<IActionResult> GetPublicKeyAsync()
        {
            return await ExecuteAsync("", async () =>
            {
                // client needs server private key first
                var model = new VCLModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString() };
                return Json(model);
            });
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

        async Task<IActionResult> ExecuteAsync<T>(T request, Func<Task<IActionResult>> func) where T : class
        {
            try
            {
                Guard.NotNull(request, nameof(request));

                if (!this.ModelState.IsValid)
                {
                    return ModelStateErrors.BuildErrorResponse(this.ModelState);
                }

                await SegWitWalletManager.WalletSemaphore.WaitAsync();
                return await func();
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception occurred: {0}", e.StackTrace);
                return ErrorHelpers.BuildErrorResponse(HttpStatusCode.BadRequest, e.Message, e.ToString());
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }

        ErrorResult BuildErrorResponse(HttpStatusCode statusCode, string message, string description = "")
        {
            var errorResponse = new ErrorResponse
            {
                Errors = new List<ErrorModel>
                {
                    new ErrorModel
                    {
                        Status = (int) statusCode,
                        Message = message,
                        Description = description
                    }
                }
            };

            return new ErrorResult((int)statusCode, errorResponse);
        }
    }
}
