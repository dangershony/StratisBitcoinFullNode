"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_info_1 = require("@shared/models/wallet-info");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var StatusBarComponent = /** @class */ (function () {
    function StatusBarComponent(apiService, globalService, genericModalService) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.genericModalService = genericModalService;
        this.connectedNodes = 0;
        this.percentSyncedNumber = 0;
        this.toolTip = '';
        this.connectedNodesTooltip = '';
    }
    StatusBarComponent.prototype.ngOnInit = function () {
        this.sidechainsEnabled = this.globalService.getSidechainEnabled();
        this.startPolling();
        this.polling = setInterval(this.startPolling.bind(this), 5000);
    };
    StatusBarComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    StatusBarComponent.prototype.getSecretInfo = function () {
        var walletInfo = new wallet_info_1.WalletInfo(this.globalService.getWalletName());
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("generalInfo", walletInfo), this.onGetSecretInfo.bind(this));
    };
    StatusBarComponent.prototype.onGetSecretInfo = function (responsePayload) {
        if (responsePayload.status === 200) {
            var generalWalletInfoResponse = responsePayload.responsePayload;
            this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
            this.chainTip = generalWalletInfoResponse.chainTip;
            this.isChainSynced = generalWalletInfoResponse.isChainSynced;
            this.connectedNodes = generalWalletInfoResponse.connectedNodes;
            var processedText = "Processed " + (this.lastBlockSyncedHeight || '0') + " out of " + this.chainTip + " blocks.";
            this.toolTip = "Synchronizing.  " + processedText;
            if (this.connectedNodes == 1) {
                this.connectedNodesTooltip = "1 connection";
            }
            else if (this.connectedNodes >= 0) {
                this.connectedNodesTooltip = this.connectedNodes + " connections";
            }
            if (!this.isChainSynced) {
                this.percentSynced = "syncing...";
            }
            else {
                this.percentSyncedNumber = ((this.lastBlockSyncedHeight / this.chainTip) * 100);
                if (this.percentSyncedNumber.toFixed(0) === "100" && this.lastBlockSyncedHeight != this.chainTip) {
                    this.percentSyncedNumber = 99;
                }
                this.percentSynced = this.percentSyncedNumber.toFixed(0) + '%';
                if (this.percentSynced === '100%') {
                    this.toolTip = "Up to date.  " + processedText;
                }
            }
        }
    };
    StatusBarComponent.prototype.getStakingInfo = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("stakingInfo", ""), this.onGetStakingInfo.bind(this));
    };
    StatusBarComponent.prototype.onGetStakingInfo = function (responsePayload) {
        if (responsePayload.status === 200) {
            var stakingResponse = responsePayload.responsePayload;
            this.stakingEnabled = stakingResponse.enabled;
        }
    };
    StatusBarComponent.prototype.startPolling = function () {
        this.getSecretInfo();
        if (!this.sidechainsEnabled) {
            this.getStakingInfo();
        }
    };
    StatusBarComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'status-bar',
            templateUrl: './status-bar.component.html',
            styleUrls: ['./status-bar.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, modal_service_1.ModalService])
    ], StatusBarComponent);
    return StatusBarComponent;
}());
exports.StatusBarComponent = StatusBarComponent;
//# sourceMappingURL=status-bar.component.js.map