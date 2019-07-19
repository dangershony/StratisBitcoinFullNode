"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_rescan_1 = require("@shared/models/wallet-rescan");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var timers_1 = require("timers");
var ResyncComponent = /** @class */ (function () {
    function ResyncComponent(globalService, apiService, genericModalService, fb) {
        this.globalService = globalService;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.fb = fb;
        this.isSyncing = true;
        this.minDate = new Date("2018-08-09");
        this.maxDate = new Date();
        this.formErrors = {
            'walletDate': ''
        };
        this.validationMessages = {
            'walletDate': {
                'required': 'Please choose the date the wallet should sync from.'
            }
        };
    }
    ResyncComponent.prototype.ngOnInit = function () {
        this.polling = setInterval(this.getGeneralInfo.bind(this), 5000);
        this.buildRescanWalletForm();
        this.bsConfig = Object.assign({}, { showWeekNumbers: false, containerClass: 'theme-dark-blue' });
    };
    ResyncComponent.prototype.ngOnDestroy = function () {
        timers_1.clearInterval(this.polling);
    };
    ResyncComponent.prototype.buildRescanWalletForm = function () {
        var _this = this;
        this.rescanWalletForm = this.fb.group({
            "walletDate": ["", forms_1.Validators.required],
        });
        this.rescanWalletForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    ResyncComponent.prototype.onValueChanged = function (data) {
        if (!this.rescanWalletForm) {
            return;
        }
        var form = this.rescanWalletForm;
        for (var field in this.formErrors) {
            this.formErrors[field] = '';
            var control = form.get(field);
            if (control && control.dirty && !control.valid) {
                var messages = this.validationMessages[field];
                for (var key in control.errors) {
                    this.formErrors[field] += messages[key] + ' ';
                }
            }
        }
    };
    ResyncComponent.prototype.onResyncClicked = function () {
        this.startRescan();
    };
    ResyncComponent.prototype.startRescan = function () {
        var rescanDate = new Date(this.rescanWalletForm.get("walletDate").value);
        rescanDate.setDate(rescanDate.getDate() - 1);
        var rescanData = new wallet_rescan_1.WalletRescan(this.walletName, rescanDate, false, true);
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("syncFromDate", rescanData), this.onStartRescan.bind(this));
    };
    ResyncComponent.prototype.onStartRescan = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.genericModalService.openModal("Resyncing", "Your wallet is now resyncing. The time remaining depends on the size and creation time of your wallet. The wallet dashboard shows your progress.");
        }
    };
    ResyncComponent.prototype.getGeneralInfo = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("generalInfo", ""), this.onGetGeneralInfo.bind(this));
    };
    ResyncComponent.prototype.onGetGeneralInfo = function (responsePayload) {
        if (responsePayload.status === 200) {
            var generalWalletInfoResponse = responsePayload.responsePayload;
            this.lastBlockSyncedHeight = generalWalletInfoResponse.lastBlockSyncedHeight;
            this.chainTip = generalWalletInfoResponse.chainTip;
            this.isChainSynced = generalWalletInfoResponse.isChainSynced;
            if (this.isChainSynced && this.lastBlockSyncedHeight === this.chainTip) {
                this.isSyncing = false;
            }
            else {
                this.isSyncing = true;
            }
        }
    };
    ResyncComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-resync',
            templateUrl: './resync.component.html',
            styleUrls: ['./resync.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, api_service_1.ApiService, modal_service_1.ModalService, forms_1.FormBuilder])
    ], ResyncComponent);
    return ResyncComponent;
}());
exports.ResyncComponent = ResyncComponent;
//# sourceMappingURL=resync.component.js.map