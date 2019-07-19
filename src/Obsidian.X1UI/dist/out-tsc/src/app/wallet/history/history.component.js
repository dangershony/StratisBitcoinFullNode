"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var router_1 = require("@angular/router");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var transaction_info_1 = require("@shared/models/transaction-info");
var transaction_details_component_1 = require("../transaction-details/transaction-details.component");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var HistoryComponent = /** @class */ (function () {
    function HistoryComponent(apiService, globalService, modalService, genericModalService, router) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.modalService = modalService;
        this.genericModalService = genericModalService;
        this.router = router;
        this.pageNumber = 1;
    }
    HistoryComponent.prototype.ngOnInit = function () {
        this.startPolling();
        this.polling = setInterval(this.startPolling.bind(this), 5000);
        this.coinUnit = this.globalService.getCoinUnit();
    };
    HistoryComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    HistoryComponent.prototype.onDashboardClicked = function () {
        this.router.navigate(['/wallet']);
    };
    HistoryComponent.prototype.openTransactionDetailDialog = function (transaction) {
        var modalRef = this.modalService.open(transaction_details_component_1.TransactionDetailsComponent, { backdrop: "static" });
        modalRef.componentInstance.transaction = transaction;
    };
    HistoryComponent.prototype.getHistory = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("history", {
            walletName: this.globalService.getWalletName()
            // take: 10
        }), this.onGetHistory.bind(this));
    };
    HistoryComponent.prototype.onGetHistory = function (responsePayload) {
        if (responsePayload.status === 200) {
            var histories = responsePayload.responsePayload.history;
            if (histories && histories.length === 1) {
                var history_1 = histories[0];
                if (history_1.transactionsHistory) {
                    this.getTransactionInfo(history_1.transactionsHistory);
                }
            }
        }
    };
    HistoryComponent.prototype.getTransactionInfo = function (transactions) {
        this.transactions = [];
        for (var _i = 0, transactions_1 = transactions; _i < transactions_1.length; _i++) {
            var transaction = transactions_1[_i];
            var transactionType = transaction.type;
            if (transaction.type === "send") {
                transactionType = "sent";
            }
            var transactionId = transaction.id;
            var transactionAmount = transaction.amount.satoshi;
            var transactionFee = void 0;
            if (transaction.fee) {
                transactionFee = transaction.fee;
            }
            else {
                transactionFee = 0;
            }
            var transactionConfirmedInBlock = transaction.confirmedInBlock;
            var transactionTimestamp = transaction.timestamp;
            this.transactions.push(new transaction_info_1.TransactionInfo(transactionType, transactionId, transactionAmount, transactionFee, transactionConfirmedInBlock, transactionTimestamp));
        }
    };
    HistoryComponent.prototype.startPolling = function () {
        this.getHistory();
    };
    HistoryComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'history-component',
            templateUrl: './history.component.html',
            styleUrls: ['./history.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, ng_bootstrap_1.NgbModal, modal_service_1.ModalService, router_1.Router])
    ], HistoryComponent);
    return HistoryComponent;
}());
exports.HistoryComponent = HistoryComponent;
//# sourceMappingURL=history.component.js.map