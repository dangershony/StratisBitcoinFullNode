using System;
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

        public SecureApiController(WalletController walletController)
        {
            this.walletController = walletController;
            CommandsWithoutWalletNameCheck = new[] { "getWalletFiles" };
        }

        [HttpPost]
        public async Task<ECCModel> ExecuteAsync([FromBody]RequestObject request)
        {
            try
            {
                if (IsRequestForPublicKey(request))
                    return CreatePublicKey();

                DecryptedRequest decryptedRequest = DecryptRequest(request, this.walletController);

                switch (decryptedRequest.Command)
                {
                    case "getWalletFiles":
                        {
                            WalletFileModel walletFileModel = await this.walletController.ListWalletsFilesAsync();
                            return CreateOk(walletFileModel, request);
                        }
                    case "loadWallet":
                        {
                            await this.walletController.LoadAsync(decryptedRequest.Target);
                            return CreateOk(request);
                        }
                    case "generalInfo":
                        {
                            WalletGeneralInfoModel walletGeneralInfoModel = await this.walletController.GetGeneralInfoAsync(decryptedRequest.Target);
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
                            AddressesModel addressesModel = await this.walletController.GetAllAddressesAsync(decryptedRequest.Target);
                            return CreateOk(addressesModel, request);
                        }

                    case "estimateFee":
                        {
                            var txFeeEstimateRequest = Deserialize<TxFeeEstimateRequest>(decryptedRequest.Payload);
                            Money fee = await this.walletController.GetTransactionFeeEstimateAsync(decryptedRequest.Target, txFeeEstimateRequest);
                            return CreateOk(fee, request);
                        }
                    case "buildTransaction":
                        {
                            var buildTransactionRequest = Deserialize<BuildTransactionRequest>(decryptedRequest.Payload);
                            WalletBuildTransactionModel walletBuildTransactionModel = await this.walletController.BuildTransactionAsync(decryptedRequest.Target, buildTransactionRequest);
                            return CreateOk(walletBuildTransactionModel, request);
                        }

                    case "sendTransaction":
                        {
                            SendTransactionRequest sendTransactionRequest = Deserialize<SendTransactionRequest>(decryptedRequest.Payload);
                            await this.walletController.SendTransactionAsync(decryptedRequest.Target, sendTransactionRequest);
                            return CreateOk(request);
                        }

                    case "syncFromDate":
                        {
                            var walletSyncFromDateRequest = Deserialize<WalletSyncFromDateRequest>(decryptedRequest.Payload);
                            await this.walletController.SyncFromDate(walletSyncFromDateRequest);
                            return CreateOk(request);
                        }
                    //case "splitCoins":
                    //case "syncFromHash":
                    //case "removeTransactions":
                    //case "spendableTransactions":
                    //case "maxBalance":
                    //case "receivedByAddress":
                    //case "verifyMessage":
                    //case "signMessage":
                    //case "createWallet":
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
