"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var coin_notation_pipe_1 = require("@shared/pipes/coin-notation.pipe");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var fee_estimation_1 = require("@shared/models/fee-estimation");
var transaction_building_1 = require("@shared/models/transaction-building");
var transaction_sending_1 = require("@shared/models/transaction-sending");
var send_confirmation_component_1 = require("./send-confirmation/send-confirmation.component");
var operators_1 = require("rxjs/operators");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var SendComponent = /** @class */ (function () {
    function SendComponent(apiService, globalService, modalService, genericModalService, activeModal, fb) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.modalService = modalService;
        this.genericModalService = genericModalService;
        this.activeModal = activeModal;
        this.fb = fb;
        this.isSending = false;
        this.estimatedFee = 0;
        this.estimatedSidechainFee = 0;
        this.totalBalance = 0;
        this.spendableBalance = 0;
        this.opReturnAmount = 1;
        this.sendFormErrors = {
            'address': '',
            'amount': '',
            'fee': '',
            'password': ''
        };
        this.sendValidationMessages = {
            'address': {
                'required': 'An address is required.',
                'minlength': 'An address is at least 26 characters long.'
            },
            'amount': {
                'required': 'An amount is required.',
                'pattern': 'Enter a valid transaction amount. Only positive numbers and no more than 8 decimals are allowed.',
                'min': "The amount has to be more or equal to 0.00001.",
                'max': 'The total transaction amount exceeds your spendable balance.'
            },
            'fee': {
                'required': 'A fee is required.'
            },
            'password': {
                'required': 'Your password is required.'
            }
        };
        this.sendToSidechainFormErrors = {
            'destionationAddress': '',
            'federationAddress': '',
            'amount': '',
            'fee': '',
            'password': ''
        };
        this.sendToSidechainValidationMessages = {
            'destinationAddress': {
                'required': 'An address is required.',
                'minlength': 'An address is at least 26 characters long.'
            },
            'federationAddress': {
                'required': 'An address is required.',
                'minlength': 'An address is at least 26 characters long.'
            },
            'amount': {
                'required': 'An amount is required.',
                'pattern': 'Enter a valid transaction amount. Only positive numbers and no more than 8 decimals are allowed.',
                'min': "The amount has to be more or equal to 1.",
                'max': 'The total transaction amount exceeds your spendable balance.'
            },
            'fee': {
                'required': 'A fee is required.'
            },
            'password': {
                'required': 'Your password is required.'
            }
        };
        this.buildSendForm();
    }
    SendComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
        if (this.sidechainEnabled) {
            this.firstTitle = "Sidechain";
            this.secondTitle = "Mainchain";
        }
        else {
            this.firstTitle = "Mainchain";
            this.secondTitle = "Sidechain";
        }
        this.startSubscriptions();
        this.coinUnit = this.globalService.getCoinUnit();
        if (this.address) {
            this.sendForm.patchValue({ 'address': this.address });
        }
    };
    SendComponent.prototype.ngOnDestroy = function () {
        this.cancelSubscriptions();
    };
    ;
    SendComponent.prototype.buildSendForm = function () {
        var _this = this;
        this.sendForm = this.fb.group({
            "address": ["", forms_1.Validators.compose([forms_1.Validators.required, forms_1.Validators.minLength(26)])],
            "amount": ["", forms_1.Validators.compose([forms_1.Validators.required, forms_1.Validators.pattern(/^([0-9]+)?(\.[0-9]{0,8})?$/), forms_1.Validators.min(0.00001), function (control) { return forms_1.Validators.max((_this.spendableBalance - _this.estimatedFee) / 100000000)(control); }])],
            "fee": ["medium", forms_1.Validators.required],
            "password": ["", forms_1.Validators.required]
        });
        this.sendForm.valueChanges.pipe(operators_1.debounceTime(300))
            .subscribe(function (data) { return _this.onSendValueChanged(data); });
    };
    SendComponent.prototype.onSendValueChanged = function (data) {
        if (!this.sendForm) {
            return;
        }
        var form = this.sendForm;
        for (var field in this.sendFormErrors) {
            this.sendFormErrors[field] = '';
            var control = form.get(field);
            if (control && control.dirty && !control.valid) {
                var messages = this.sendValidationMessages[field];
                for (var key in control.errors) {
                    this.sendFormErrors[field] += messages[key] + ' ';
                }
            }
        }
        this.apiError = "";
        if (this.sendForm.get("address").valid && this.sendForm.get("amount").valid) {
            this.estimateFee();
        }
    };
    SendComponent.prototype.getMaxBalance = function () {
        var _this = this;
        var data = {
            walletName: this.globalService.getWalletName(),
            feeType: this.sendForm.get("fee").value
        };
        var balanceResponse;
        this.apiService.getMaximumBalance(data)
            .subscribe(function (response) {
            balanceResponse = response;
        }, function (error) {
            _this.apiError = error.error.errors[0].message;
        }, function () {
            _this.sendForm.patchValue({ amount: +new coin_notation_pipe_1.CoinNotationPipe().transform(balanceResponse.maxSpendableAmount) });
            _this.estimatedFee = balanceResponse.fee;
        });
    };
    ;
    SendComponent.prototype.estimateFee = function () {
        var feeEstimation = new fee_estimation_1.FeeEstimation(null, null, this.sendForm.get("address").value.trim(), this.sendForm.get("amount").value, this.sendForm.get("fee").value, true);
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("estimateFee", feeEstimation), this.onEstimateFee.bind(this));
    };
    SendComponent.prototype.onEstimateFee = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.estimatedFee = responsePayload.responsePayload.satoshi;
        }
        else {
            this.apiError = responsePayload.statusText;
        }
    };
    SendComponent.prototype.buildTransaction = function () {
        this.transaction = new transaction_building_1.TransactionBuilding(this.globalService.getWalletName(), "account 0", this.sendForm.get("password").value, this.sendForm.get("address").value.trim(), this.sendForm.get("amount").value, 
        //this.sendForm.get("fee").value,
        // TO DO: use coin notation
        this.estimatedFee / 100000000, true, false);
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("buildTransaction", this.transaction), this.onBuildTransaction.bind(this));
    };
    ;
    SendComponent.prototype.onBuildTransaction = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.estimatedFee = responsePayload.responsePayload.fee;
            this.transactionHex = responsePayload.responsePayload.hex;
            if (this.isSending) {
                this.hasOpReturn = false;
                this.sendTransaction(this.transactionHex);
            }
        }
        else {
            this.isSending = false;
            this.apiError = responsePayload.statusText;
        }
    };
    SendComponent.prototype.send = function () {
        this.isSending = true;
        this.buildTransaction();
    };
    ;
    SendComponent.prototype.sendTransaction = function (hex) {
        var transaction = new transaction_sending_1.TransactionSending(hex);
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("sendTransaction", transaction), this.onSendTransaction.bind(this));
    };
    SendComponent.prototype.onSendTransaction = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.activeModal.close("Close clicked");
            this.openConfirmationModal();
        }
        else {
            this.isSending = false;
            this.apiError = responsePayload.statusText;
        }
    };
    SendComponent.prototype.getWalletBalance = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("balance", ""), this.onGetWalletBalance.bind(this));
    };
    SendComponent.prototype.onGetWalletBalance = function (responsePayload) {
        if (responsePayload.status === 200) {
            var walletBalance = responsePayload.responsePayload;
            this.totalBalance = walletBalance.amountConfirmed.satoshi + walletBalance.amountUnconfirmed.satoshi;
            this.spendableBalance = walletBalance.spendableAmount.satoshi;
        }
    };
    SendComponent.prototype.openConfirmationModal = function () {
        var modalRef = this.modalService.open(send_confirmation_component_1.SendConfirmationComponent, { backdrop: "static" });
        modalRef.componentInstance.transaction = this.transaction;
        modalRef.componentInstance.transactionFee = this.estimatedFee ? this.estimatedFee : this.estimatedSidechainFee;
        modalRef.componentInstance.sidechainEnabled = this.sidechainEnabled;
        modalRef.componentInstance.opReturnAmount = this.opReturnAmount;
        modalRef.componentInstance.hasOpReturn = this.hasOpReturn;
    };
    SendComponent.prototype.cancelSubscriptions = function () {
        if (this.walletBalanceSubscription) {
            this.walletBalanceSubscription.unsubscribe();
        }
    };
    ;
    SendComponent.prototype.startSubscriptions = function () {
        this.getWalletBalance();
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String)
    ], SendComponent.prototype, "address", void 0);
    SendComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'send-component',
            templateUrl: './send.component.html',
            styleUrls: ['./send.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, ng_bootstrap_1.NgbModal, modal_service_1.ModalService, ng_bootstrap_1.NgbActiveModal, forms_1.FormBuilder])
    ], SendComponent);
    return SendComponent;
}());
exports.SendComponent = SendComponent;
//# sourceMappingURL=send.component.js.map