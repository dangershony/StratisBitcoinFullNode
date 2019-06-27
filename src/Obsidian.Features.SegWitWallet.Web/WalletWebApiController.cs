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
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Helpers;
using Stratis.Bitcoin.Features.Wallet.Models;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Utilities.JsonErrors;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.SegWitWallet.Web
{
    /// <summary>
    /// ApiController providing operations on a wallet.
    /// </summary>
    [Route("api/segwitwallet")]
    public class WalletWebApiController : Controller
    {
        readonly SegWitWalletController segWitWalletController;
        readonly ILogger logger;

        public WalletWebApiController(SegWitWalletController segWitWalletController, ILoggerFactory loggerFactory)
        {
            this.segWitWalletController = segWitWalletController;
            this.logger = loggerFactory.CreateLogger(typeof(WalletWebApiController).FullName);
        }

        /// <summary>
        /// Creates a new wallet on this full node.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to create a wallet.</param>
        /// <returns>A JSON object containing the mnemonic created for the new wallet.</returns>
        [Route("create")]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody]RequestObject<WalletCreationRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.CreateAsync);
        }

        /// <summary>
        /// Signs a message and returns the signature.
        /// </summary>
        /// <param name="request">The object containing the parameters used to sign a message.</param>
        /// <returns>A JSON object containing the generated signature.</returns>
        [Route("signmessage")]
        [HttpPost]
        public async Task<IActionResult> SignMessageAsync([FromBody]RequestObject<SignMessageRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.SignMessageAsync);

           
        }

        /// <summary>
        /// Verifies the signature of a message.
        /// </summary>
        /// <param name="request">The object containing the parameters verify a signature.</param>
        /// <returns>A JSON object containing the result of the verification.</returns>
        [Route("verifymessage")]
        [HttpPost]
        public async Task<IActionResult> VerifyMessageAsync([FromBody]RequestObject<VerifyRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.VerifyMessageAsync);
        }

        /// <summary>
        /// Loads a previously created wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to load an existing wallet</param>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [Route("load")]
        [HttpPost]
        public async Task<IActionResult> LoadAsync([FromBody]RequestObject<WalletLoadRequest> request)
        {
            return await ExecuteRequestAsyncVoid(request, this.segWitWalletController.LoadAsync);
        }

        /// <summary>
        /// Recovers an existing wallet.
        /// </summary>
        /// <param name="request">An object containing the parameters used to recover a wallet.</param>
        /// <returns>A value of Ok if the wallet was successfully recovered.</returns>
        [Route("recover")]
        [HttpPost]
        public async Task<IActionResult> RecoverAsync([FromBody]RequestObject<WalletRecoveryRequest> request)
        {
            return await ExecuteRequestAsyncVoid(request, this.segWitWalletController.RecoverAsync);
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
        public async Task<IActionResult> GetGeneralInfoAsync([FromQuery] RequestObject<WalletName> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetGeneralInfoAsync);
        }

        /// <summary>
        /// Gets the history of a wallet. This includes the transactions held by the entire wallet
        /// or a single account if one is specified. 
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's history.</param>
        /// <returns>A JSON object containing the wallet history.</returns>
        [Route("history")]
        [HttpGet]
        public async Task<IActionResult> GetHistoryAsync([FromQuery] RequestObject<WalletHistoryRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetHistoryAsync);
        }

        /// <summary>
        /// Gets the balance of a wallet in STRAT (or sidechain coin). Both the confirmed and unconfirmed balance are returned.
        /// </summary>
        /// <param name="request">An object containing the parameters used to retrieve a wallet's balance.</param>
        /// <returns>A JSON object containing the wallet balance.</returns>
        [Route("balance")]
        [HttpGet]
        public async Task<IActionResult> GetBalanceAsync([FromQuery] RequestObject<WalletBalanceRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetBalanceAsync);
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
        public async Task<IActionResult> GetReceivedByAddressAsync([FromQuery] RequestObject<ReceivedByAddressRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetReceivedByAddressAsync);
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
        public async Task<IActionResult> GetMaximumSpendableBalanceAsync([FromQuery] RequestObject<WalletMaximumBalanceRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetMaximumSpendableBalanceAsync);
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
        public async Task<IActionResult> GetSpendableTransactionsAsync([FromQuery] RequestObject<SpendableTransactionsRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetSpendableTransactionsAsync);
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
        public async Task<IActionResult> GetTransactionFeeEstimateAsync([FromBody]RequestObject<TxFeeEstimateRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetTransactionFeeEstimateAsync);
        }

        /// <summary>
        /// Builds a transaction and returns the hex to use when executing the transaction.
        /// </summary>
        /// <param name="request">An object containing the parameters used to build a transaction.</param>
        /// <returns>A JSON object including the transaction ID, the hex used to execute
        /// the transaction, and the transaction fee.</returns>
        [Route("build-transaction")]
        [HttpPost]
        public async Task<IActionResult> BuildTransactionAsync([FromBody] RequestObject<BuildTransactionRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.BuildTransactionAsync);
        }

        /// <summary>
        /// Sends a transaction that has already been built.
        /// Use the /api/Wallet/build-transaction call to create transactions.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters used to a send transaction request.</param>
        /// <returns>A JSON object containing information about the sent transaction.</returns>
        [Route("send-transaction")]
        [HttpPost]
        public async Task<IActionResult> SendTransactionAsync([FromBody] RequestObject<SendTransactionRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.SendTransactionAsync);
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
            return await ExecuteRequestAsyncFun(new RequestObject(), this.segWitWalletController.ListWalletsFilesAsync);
        }

        /// <summary>
        /// Gets a list of accounts for the specified wallet.
        /// </summary>
        /// <param name="request">An object containing the necessary parameters to list the accounts for a wallet.</param>
        /// <returns>A JSON object containing a list of accounts for the specified wallet.</returns>
        [Route("accounts")]
        [HttpGet]
        public async Task<IActionResult> ListAccountsAsync([FromQuery]RequestObject<ListAccountsModel> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.ListAccountsAsync);
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
        public async Task<IActionResult> GetUnusedAddressAsync([FromQuery]RequestObject<GetUnusedAddressModel> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetUnusedAddressAsync);
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
        public async Task<IActionResult> GetUnusedAddresses([FromQuery]RequestObject<GetUnusedAddressesModel> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetUnusedAddresses);
        }

        /// <summary>
        /// Gets all addresses for a wallet account.
        /// <param name="request">An object containing the necessary parameters to retrieve
        /// all addresses for a wallet account.</param>
        /// <returns>A JSON object containing all addresses for a wallet account (in Base58 format).</returns>
        /// </summary>
        [Route("addresses")]
        [HttpGet]
        public async Task<IActionResult> GetAllAddressesAsync([FromQuery]RequestObject<GetAllAddressesModel> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.GetAllAddressesAsync);
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
        public async Task<IActionResult> RemoveTransactionsAsync([FromQuery]RequestObject<RemoveTransactionsModel> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.RemoveTransactionsAsync);
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
        public async Task<IActionResult> SyncAsync([FromBody] RequestObject<HashModel> request)
        {
            return await ExecuteRequestAsyncVoid(request, this.segWitWalletController.SyncAsync);
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
        public async Task<IActionResult> SyncFromDate([FromBody] RequestObject<WalletSyncFromDateRequest> request)
        {
            return await ExecuteRequestAsyncVoid(request, this.segWitWalletController.SyncFromDate);
        }

        /// <summary>
        /// Creates requested amount of UTXOs each of equal value.
        /// </summary>
        /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
        [HttpPost]
        [Route("splitcoins")]
        public async Task<IActionResult> SplitCoinsAsync([FromBody] RequestObject<SplitCoinsRequest> request)
        {
            return await ExecuteRequestAsync(request, this.segWitWalletController.SplitCoinsAsync);
        }

        /// <summary>
        /// Provides the server's public key to the client.
        /// </summary>
        /// <returns>Server public key.</returns>
        [HttpGet]
        [Route("getpublickey")]
        public async Task<IActionResult> GetPublicKeyAsync()
        {
            return await ExecuteRequestAsyncFun(new RequestObject(), async () =>
            {
                // client needs server private key first
                var model = new VCLModel { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString() };
                return Json(model);
            });
        }



        async Task<IActionResult> ExecuteRequestAsync<T, TResult>(RequestObject<T> request, Func<T, Task<TResult>> coreControllerAction) where T : class
        {
            try
            {
                Guard.NotNull(request, nameof(request));
                Guard.NotNull(request.VCLModel, nameof(request));
                Guard.NotNull(request.VCLModel.CurrentPublicKey, nameof(request));

                string requestJson = Decrpyt(request);
                T requestPayload = JsonConvert.DeserializeObject<T>(requestJson);

                await SegWitWalletManager.WalletSemaphore.WaitAsync();

                var coreResult = coreControllerAction(requestPayload);

                string jsonString = JsonConvert.SerializeObject(coreResult);

                string cipher = "Encrypt()";
                var model = new VCLModel
                { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(), CipherV2Bytes = cipher };
                return Json(model);

            }
            catch (SegWitWalletException se)
            {
                this.logger.LogError(se.ToString());
                return BuildErrorResponse(se.HttpStatusCode, se.Message);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return BuildErrorResponse(HttpStatusCode.BadRequest, e.Message);
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }

        async Task<IActionResult> ExecuteRequestAsyncFun<TResult>(RequestObject request, Func<Task<TResult>> coreControllerAction) 
        {
            try
            {
                Guard.NotNull(request, nameof(request));
                Guard.NotNull(request.VCLModel, nameof(request));
                Guard.NotNull(request.VCLModel.CurrentPublicKey, nameof(request));

                throw new NotImplementedException();
                //string requestJson = ""; // Check only, no payload, but authenticate Decrpyt(request);
                //T requestPayload = JsonConvert.DeserializeObject<T>(requestJson);

                //await SegWitWalletManager.WalletSemaphore.WaitAsync();

                //var coreResult = coreControllerAction(requestPayload);

                //string jsonString = JsonConvert.SerializeObject(coreResult);

                //string cipher = "Encrypt()";
                //var model = new VCLModel
                //    { CurrentPublicKey = VCL.ECKeyPair.PublicKey.ToHexString(), CipherV2Bytes = cipher };
                //return Json(model);

            }
            catch (SegWitWalletException se)
            {
                this.logger.LogError(se.ToString());
                return BuildErrorResponse(se.HttpStatusCode, se.Message);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return BuildErrorResponse(HttpStatusCode.BadRequest, e.Message);
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }

        async Task<IActionResult> ExecuteRequestAsyncVoid<T>(RequestObject<T> request, Func<T,Task> coreControllerAction) where T : class
        {
            try
            {
                Guard.NotNull(request, nameof(request));
                Guard.NotNull(request.VCLModel, nameof(request));
                Guard.NotNull(request.VCLModel.CurrentPublicKey, nameof(request));

                string requestJson = Decrpyt(request);
                T requestPayload = JsonConvert.DeserializeObject<T>(requestJson);

                await SegWitWalletManager.WalletSemaphore.WaitAsync();

                var _ = coreControllerAction(requestPayload);
                return Ok();
            }
            catch (SegWitWalletException se)
            {
                this.logger.LogError(se.ToString());
                return BuildErrorResponse(se.HttpStatusCode, se.Message);
            }
            catch (Exception e)
            {
                this.logger.LogError(e.ToString());
                return BuildErrorResponse(HttpStatusCode.BadRequest, e.Message);
            }
            finally
            {
                SegWitWalletManager.WalletSemaphore.Release();
            }
        }

        string Decrpyt<T>(RequestObject<T> request) where T : class
        {
            return "json here";
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
