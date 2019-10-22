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
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
using VisualCrypt.VisualCryptLight;
using BuildTransactionRequest = Obsidian.Features.X1Wallet.Transactions.BuildTransactionRequest;

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
                    case "getWalletFiles":
                        {
                            WalletFileModel walletFileModel = this.walletController.ListWalletsFiles();
                            return CreateOk(walletFileModel, request);
                        }
                    case "loadWallet":
                        {
                            LoadWalletResponse loadWalletResponse = this.walletController.LoadWallet();
                            return CreateOk(loadWalletResponse, request);
                        }
                    case "generalInfo":
                        {
                            GetWalletInfoResponse walletGetWalletInfoResponse = this.walletController.GetWalletInfo();
                            return CreateOk(walletGetWalletInfoResponse, request);
                        }
                    case "nodeInfo":
                        {
                            NodeInfo nodeStatus = this.walletController.GetNodeInfo();
                            return CreateOk(nodeStatus, request);
                        }

                    case "balance":
                        {
                            Balance balanceModel = this.walletController.GetBalance();
                            return CreateOk(balanceModel, request);
                        }

                    case "history":
                        {
                            // Deprecated
                            var walletHistoryRequest = Deserialize<WalletHistoryRequest>(decryptedRequest.Payload);
                            return CreateOk(new WalletHistoryModel(), request);
                        }

                    case "stakingInfo":
                        {
                            StakingInfo stakingInfo = this.walletController.GetStakingInfo();
                            return CreateOk(stakingInfo, request);
                        }

                    case "getReceiveAddresses":
                        {
                            // this command will only return one unused address or throw if the wallet is out of unused addresses
                            var addressesModel = this.walletController.GetUnusedReceiveAddresses();
                            return CreateOk(addressesModel, request);
                        }

                    case "estimateFee":
                        {
                            var txFeeEstimateRequest = Deserialize<BuildTransactionRequest>(decryptedRequest.Payload);
                            long fee = this.walletController.EstimateFee(txFeeEstimateRequest);
                            return CreateOk(fee, request);
                        }
                    case "buildTransaction":
                        {
                            var buildTransactionRequest = Deserialize<BuildTransactionRequest>(decryptedRequest.Payload);
                            BuildTransactionResponse buildTransactionResponse = this.walletController.BuildTransaction(buildTransactionRequest);
                            return CreateOk(buildTransactionResponse, request);
                        }

                    case "sendTransaction":
                        {
                            SendHexTransactionRequest sendTransactionRequest = Deserialize<SendHexTransactionRequest>(decryptedRequest.Payload);
                            this.walletController.SendTransaction(sendTransactionRequest);
                            return CreateOk(request);
                        }

                    case "buildAndSendTransaction":
                        {
                            var buildTransactionRequest = Deserialize<BuildTransactionRequest>(decryptedRequest.Payload);
                            BuildTransactionResponse buildTransactionResponse = this.walletController.BuildTransaction(buildTransactionRequest);
                            this.walletController.SendTransaction(new SendHexTransactionRequest { Hex = buildTransactionResponse.Transaction.ToHex() });
                            return CreateOk(buildTransactionResponse, request);
                        }

                    case "syncFromDate":
                        {
                            var walletSyncFromDateRequest = Deserialize<WalletSyncFromDateRequest>(decryptedRequest.Payload);
                            this.walletController.SyncFromDate(walletSyncFromDateRequest);
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
