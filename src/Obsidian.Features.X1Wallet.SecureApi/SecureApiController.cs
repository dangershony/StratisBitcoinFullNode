using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Api.Responses;
using Obsidian.Features.X1Wallet.SecureApi.Models;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Transactions;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.SecureApi
{
    public class SecureApiController : SecureApiControllerBase
    {
        readonly WalletController walletController;
        readonly SecureApiSettings secureApiSettings;

        public SecureApiController(WalletController walletController, SecureApiSettings secureApiSettings)
        {
            this.walletController = walletController;
            this.secureApiSettings = secureApiSettings;
            CommandsWithoutWalletNameCheck = new[] { "createWallet", "getWalletFiles" };
        }

        [HttpPost]
        public async Task<ECCModel> ExecuteAsync([FromBody]RequestObject request)
        {
            if (AuthKey == null)
            {
                this.Response.StatusCode = 403;
                return null;
            }

            try
            {
                if (IsRequestForPublicKey(request))
                    return CreatePublicKey();

                DecryptedRequest decryptedRequest = DecryptRequest(request, this.walletController);
                CheckPermissions(decryptedRequest, this.secureApiSettings);

                switch (decryptedRequest.Command)
                {

                    case "createWallet":
                        {
                            WalletCreateRequest walletCreateRequest = Deserialize<WalletCreateRequest>(decryptedRequest.Payload);
                            this.walletController.CreateWallet(walletCreateRequest);
                            return CreateOk(request);
                        }
                    case "loadWallet":
                        {
                            LoadWalletResponse loadWalletResponse = this.walletController.LoadWallet();
                            return CreateOk(loadWalletResponse, request);
                        }
                    case "daemonInfo": // one-time information about the running daemon, includes list of wallet files
                        {
                            DaemonInfo daemonStatus = this.walletController.GetDaemonInfo();
                            return CreateOk(daemonStatus, request);
                        }
                    case "walletInfo": // does not depend on weather a wallet is loaded, but if a wallet is loaded, it includes all wallet info, including balance and staking info when enabled
                        {
                            WalletInfo walletInfo = this.walletController.GetWalletInfo();
                            return CreateOk(walletInfo, request);
                        }
                    case "historyInfo":
                        {
                            var historyRequest = Deserialize<HistoryRequest>(decryptedRequest.Payload);
                            HistoryInfo historyInfo = this.walletController.GetHistoryInfo(historyRequest);
                            return CreateOk(historyInfo, request);
                        }

                    case "getReceiveAddresses":
                        {
                            // this command will only return one unused address or throw if the wallet is out of unused addresses
                            var addressesModel = this.walletController.GetUnusedReceiveAddresses();
                            return CreateOk(addressesModel, request);
                        }
                    case "estimateFee":
                        {
                            var txFeeEstimateRequest = Deserialize<TransactionRequest>(decryptedRequest.Payload);
                            long fee = this.walletController.EstimateFee(txFeeEstimateRequest);
                            return CreateOk(fee, request);
                        }
                    case "buildTransaction":
                        {
                            var buildTransactionRequest = Deserialize<TransactionRequest>(decryptedRequest.Payload);
                            TransactionResponse transactionResponse = this.walletController.BuildTransaction(buildTransactionRequest);
                            return CreateOk(transactionResponse, request);
                        }

                    case "repair":
                        {
                            var walletSyncFromDateRequest = Deserialize<RepairRequest>(decryptedRequest.Payload);
                            this.walletController.Repair(walletSyncFromDateRequest);
                            return CreateOk(request);
                        }

                    case "importKeys":
                        {
                            var importKeysRequest = Deserialize<ImportKeysRequest>(decryptedRequest.Payload);
                            var importKeysResponse = this.walletController.ImportKeys(importKeysRequest);
                            return CreateOk(importKeysResponse, request);
                        }
                    case "exportKeys":
                        {
                            var exportKeysRequest = Deserialize<ExportKeysRequest>(decryptedRequest.Payload);
                            var exportKeysResponse = this.walletController.ExportKeys(exportKeysRequest);
                            return CreateOk(exportKeysResponse, request);
                        }
                    case "startStaking":
                        {
                            var startStakingRequest = Deserialize<StartStakingRequest>(decryptedRequest.Payload);
                            this.walletController.StartStaking(startStakingRequest);
                            return CreateOk(request);
                        }
                    case "stopStaking":
                        {
                            this.walletController.StopStaking();
                            return CreateOk(request);
                        }
                    default:
                        throw new NotSupportedException($"The command '{decryptedRequest.Command}' is not supported.");
                }
            }
            catch (Exception e)
            {
                return CreateError(e, request);
            }
        }

    }
}
