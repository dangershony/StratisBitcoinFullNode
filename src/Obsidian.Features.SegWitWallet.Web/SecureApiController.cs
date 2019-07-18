﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.SecureApi.Models;
using Stratis.Bitcoin.Controllers.Models;
using Stratis.Bitcoin.Features.Miner.Models;
using Stratis.Bitcoin.Features.Wallet.Models;
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
                            await this.walletController.CreateKeyWalletAsync(walletCreateRequest);
                            return CreateOk(request);
                        }
                    case "getWalletFiles":
                        {
                            WalletFileModel walletFileModel = await this.walletController.ListWalletsFilesAsync();
                            return CreateOk(walletFileModel, request);
                        }
                    case "loadWallet":
                        {
                            await this.walletController.LoadAsync();
                            return CreateOk(request);
                        }
                    case "generalInfo":
                        {
                            WalletGeneralInfoModel walletGeneralInfoModel = await this.walletController.GetGeneralInfoAsync();
                            return CreateOk(walletGeneralInfoModel, request);
                        }
                    case "nodeStatus":
                        {
                            StatusModel nodeStatus = this.walletController.GetNodeStatus();
                            return CreateOk(nodeStatus, request);
                        }

                    case "balance":
                        {
                            Balance balanceModel = await this.walletController.GetBalanceAsync(decryptedRequest.Target);
                            return CreateOk(balanceModel, request);
                        }

                    case "history":
                        {
                            var walletHistoryRequest = Deserialize<WalletHistoryRequest>(decryptedRequest.Payload);
                            WalletHistoryModel walletHistoryModel = await this.walletController.GetHistoryAsync(walletHistoryRequest);
                            return CreateOk(walletHistoryModel, request);
                        }

                    case "stakingInfo":
                        {
                            GetStakingInfoModel stakingInfo = this.walletController.GetStakingInfo();
                            return CreateOk(stakingInfo, request);
                        }

                    case "getReceiveAddresses":
                        {
                            var addressesModel = await this.walletController.GetAllAddressesAsync();
                            return CreateOk(addressesModel, request);
                        }

                    case "estimateFee":
                        {
                            var txFeeEstimateRequest = Deserialize<TxFeeEstimateRequest>(decryptedRequest.Payload);
                            Money fee = await this.walletController.GetTransactionFeeEstimateAsync(txFeeEstimateRequest);
                            return CreateOk(fee, request);
                        }
                    case "buildTransaction":
                        {
                            var buildTransactionRequest = Deserialize<BuildTransactionRequest>(decryptedRequest.Payload);
                            WalletBuildTransactionModel walletBuildTransactionModel = await this.walletController.BuildTransactionAsync(buildTransactionRequest);
                            return CreateOk(walletBuildTransactionModel, request);
                        }

                    case "sendTransaction":
                        {
                            SendTransactionRequest sendTransactionRequest = Deserialize<SendTransactionRequest>(decryptedRequest.Payload);
                            await this.walletController.SendTransactionAsync(sendTransactionRequest);
                            return CreateOk(request);
                        }

                    case "syncFromDate":
                        {
                            var walletSyncFromDateRequest = Deserialize<WalletSyncFromDateRequest>(decryptedRequest.Payload);
                            await this.walletController.SyncFromDate(walletSyncFromDateRequest);
                            return CreateOk(request);
                        }

                    case "importKeys":
                        {
                            var importKeysRequest = Deserialize<ImportKeysRequest>(decryptedRequest.Payload);
                            var importKeysResponse = await this.walletController.ImportKeysAsync(importKeysRequest);
                            return CreateOk(importKeysResponse, request);
                        }
                    case "exportKeys":
                    {
                        var exportKeysRequest = Deserialize<ExportKeysRequest>(decryptedRequest.Payload);
                        var exportKeysResponse = await this.walletController.ExportKeysAsync(exportKeysRequest);
                        return CreateOk(exportKeysResponse, request);
                    }
                    case "startStaking":
                        {
                            var startStakingRequest = Deserialize<StartStakingRequest>(decryptedRequest.Payload);
                            await this.walletController.StartStaking(startStakingRequest);
                            return CreateOk(request);
                        }
                    case "stopStaking":
                    {
                        await this.walletController.StopStaking();
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