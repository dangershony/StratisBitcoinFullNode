"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_info_1 = require("@shared/models/wallet-info");
var transaction_info_1 = require("@shared/models/transaction-info");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var TransactionDetailsComponent = /** @class */ (function () {
    function TransactionDetailsComponent(apiService, globalService, genericModalService, activeModal) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.genericModalService = genericModalService;
        this.activeModal = activeModal;
        this.copied = false;
    }
    TransactionDetailsComponent.prototype.ngOnInit = function () {
        this.getGeneralInfo();
        this.polling = setInterval(this.getGeneralInfo.bind(this), 5000);
        this.coinUnit = this.globalService.getCoinUnit();
    };
    TransactionDetailsComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    TransactionDetailsComponent.prototype.onCopiedClick = function () {
        this.copied = true;
    };
    TransactionDetailsComponent.prototype.getGeneralInfo = function () {
        var walletInfo = new wallet_info_1.WalletInfo(this.globalService.getWalletName());
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("generalInfo", walletInfo), this.onGetGeneralInfo.bind(this));
    };
    TransactionDetailsComponent.prototype.onGetGeneralInfo = function (responsePayload) {
        if (responsePayload.status === 200) {
            var generalWalletInfoResponse = responsePayload.responsePayload;
            this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
            this.getConfirmations(this.transaction);
        }
    };
    TransactionDetailsComponent.prototype.getConfirmations = function (transaction) {
        if (transaction.transactionConfirmedInBlock) {
            this.confirmations = this.lastBlockSyncedHeight - Number(transaction.transactionConfirmedInBlock) + 1;
        }
        else {
            this.confirmations = 0;
        }
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", transaction_info_1.TransactionInfo)
    ], TransactionDetailsComponent.prototype, "transaction", void 0);
    TransactionDetailsComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'transaction-details',
            templateUrl: './transaction-details.component.html',
            styleUrls: ['./transaction-details.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, modal_service_1.ModalService, ng_bootstrap_1.NgbActiveModal])
    ], TransactionDetailsComponent);
    return TransactionDetailsComponent;
}());
exports.TransactionDetailsComponent = TransactionDetailsComponent;
//# sourceMappingURL=transaction-details.component.js.map