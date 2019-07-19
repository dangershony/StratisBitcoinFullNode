"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var forms_1 = require("@angular/forms");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var transaction_info_1 = require("@shared/models/transaction-info");
var send_component_1 = require("../send/send.component");
var receive_component_1 = require("../receive/receive.component");
var transaction_details_component_1 = require("../transaction-details/transaction-details.component");
var router_1 = require("@angular/router");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var DashboardComponent = /** @class */ (function () {
    function DashboardComponent(apiService, globalService, modalService, genericModalService, router, fb) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.modalService = modalService;
        this.genericModalService = genericModalService;
        this.router = router;
        this.fb = fb;
        this.awaitingMaturity = 0;
        this.hasBalance = false;
        this.buildStakingForm();
    }
    DashboardComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
        this.walletName = this.globalService.getWalletName();
        this.coinUnit = this.globalService.getCoinUnit();
        this.startPolling();
        this.polling = setInterval(this.startPolling.bind(this), 5000);
    };
    ;
    DashboardComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    DashboardComponent.prototype.buildStakingForm = function () {
        this.stakingForm = this.fb.group({
            "walletPassword": ["", forms_1.Validators.required]
        });
    };
    DashboardComponent.prototype.goToHistory = function () {
        this.router.navigate(['/segwitwallet/history']);
    };
    DashboardComponent.prototype.openSendDialog = function () {
        var modalRef = this.modalService.open(send_component_1.SendComponent, { backdrop: "static", keyboard: false });
    };
    DashboardComponent.prototype.openReceiveDialog = function () {
        var modalRef = this.modalService.open(receive_component_1.ReceiveComponent, { backdrop: "static", keyboard: false });
    };
    ;
    DashboardComponent.prototype.openTransactionDetailDialog = function (transaction) {
        var modalRef = this.modalService.open(transaction_details_component_1.TransactionDetailsComponent, { backdrop: "static", keyboard: false });
        modalRef.componentInstance.transaction = transaction;
    };
    DashboardComponent.prototype.getWalletBalance = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("balance", {
            walletName: this.globalService.getWalletName(),
            accountName: "account 0"
        }), this.onGetWalletBalance.bind(this));
    };
    DashboardComponent.prototype.onGetWalletBalance = function (responsePayload) {
        if (responsePayload.status === 200) {
            var walletBalance = responsePayload.responsePayload;
            this.confirmedBalance = walletBalance.amountConfirmed.satoshi;
            this.unconfirmedBalance = walletBalance.amountUnconfirmed.satoshi;
            this.spendableBalance = walletBalance.spendableAmount.satoshi;
            if ((this.confirmedBalance + this.unconfirmedBalance) > 0) {
                this.hasBalance = true;
            }
            else {
                this.hasBalance = false;
            }
        }
    };
    DashboardComponent.prototype.getHistory = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("history", {
            walletName: this.globalService.getWalletName(),
            take: 10
        }), this.onGetHistory.bind(this));
    };
    DashboardComponent.prototype.onGetHistory = function (responsePayload) {
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
    DashboardComponent.prototype.getTransactionInfo = function (transactions) {
        this.transactionArray = [];
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
            this.transactionArray.push(new transaction_info_1.TransactionInfo(transactionType, transactionId, transactionAmount, transactionFee, transactionConfirmedInBlock, transactionTimestamp));
        }
    };
    DashboardComponent.prototype.startStaking = function () {
        this.isStarting = true;
        this.isStopping = false;
        var walletData = {
            name: this.globalService.getWalletName(),
            password: this.stakingForm.get('walletPassword').value
        };
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("startStaking", { password: walletData.password }), this.onStartStaking.bind(this));
    };
    DashboardComponent.prototype.onStartStaking = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.stakingEnabled = true;
            this.stakingForm.patchValue({ walletPassword: "" });
        }
        else {
            this.isStarting = false;
            this.stakingEnabled = false;
            this.stakingForm.patchValue({ walletPassword: "" });
        }
    };
    DashboardComponent.prototype.stopStaking = function () {
        this.isStopping = true;
        this.isStarting = false;
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("stopStaking", ""), this.onStopStaking.bind(this));
    };
    DashboardComponent.prototype.onStopStaking = function (responsePayload) {
        if (responsePayload.status === 200) {
        }
        this.stakingEnabled = false;
    };
    DashboardComponent.prototype.getStakingInfo = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("stakingInfo", ""), this.onGetStakingInfo.bind(this));
    };
    DashboardComponent.prototype.onGetStakingInfo = function (responsePayload) {
        if (responsePayload.status === 200) {
            var stakingResponse = responsePayload.responsePayload;
            this.stakingEnabled = stakingResponse.enabled;
            this.stakingActive = stakingResponse.staking;
            this.stakingWeight = stakingResponse.weight;
            this.netStakingWeight = stakingResponse.netStakeWeight;
            this.awaitingMaturity = (this.unconfirmedBalance + this.confirmedBalance) - this.spendableBalance;
            this.expectedTime = stakingResponse.expectedTime;
            this.dateTime = this.secondsToString(this.expectedTime);
            if (this.stakingActive) {
                this.isStarting = false;
            }
            else {
                this.isStopping = false;
            }
        }
    };
    DashboardComponent.prototype.secondsToString = function (seconds) {
        var numDays = Math.floor(seconds / 86400);
        var numHours = Math.floor((seconds % 86400) / 3600);
        var numMinutes = Math.floor(((seconds % 86400) % 3600) / 60);
        var numSeconds = ((seconds % 86400) % 3600) % 60;
        var dateString = "";
        if (numDays > 0) {
            if (numDays > 1) {
                dateString += numDays + " days ";
            }
            else {
                dateString += numDays + " day ";
            }
        }
        if (numHours > 0) {
            if (numHours > 1) {
                dateString += numHours + " hours ";
            }
            else {
                dateString += numHours + " hour ";
            }
        }
        if (numMinutes > 0) {
            if (numMinutes > 1) {
                dateString += numMinutes + " minutes ";
            }
            else {
                dateString += numMinutes + " minute ";
            }
        }
        if (dateString === "") {
            dateString = "Unknown";
        }
        return dateString;
    };
    DashboardComponent.prototype.cancelSubscriptions = function () {
        if (this.walletBalanceSubscription) {
            this.walletBalanceSubscription.unsubscribe();
        }
        if (this.walletHistorySubscription) {
            this.walletHistorySubscription.unsubscribe();
        }
        if (this.stakingInfoSubscription) {
            this.stakingInfoSubscription.unsubscribe();
        }
    };
    DashboardComponent.prototype.startPolling = function () {
        this.getWalletBalance();
        this.getHistory();
        if (!this.sidechainEnabled) {
            this.getStakingInfo();
        }
    };
    DashboardComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'dashboard-component',
            templateUrl: './dashboard.component.html',
            styleUrls: ['./dashboard.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, ng_bootstrap_1.NgbModal, modal_service_1.ModalService, router_1.Router, forms_1.FormBuilder])
    ], DashboardComponent);
    return DashboardComponent;
}());
exports.DashboardComponent = DashboardComponent;
//# sourceMappingURL=dashboard.component.js.map