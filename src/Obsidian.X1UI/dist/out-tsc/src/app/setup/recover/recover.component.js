"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var router_1 = require("@angular/router");
var global_service_1 = require("@shared/services/global.service");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_recovery_1 = require("@shared/models/wallet-recovery");
var RecoverComponent = /** @class */ (function () {
    function RecoverComponent(globalService, apiService, genericModalService, router, fb) {
        this.globalService = globalService;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.router = router;
        this.fb = fb;
        this.isRecovering = false;
        this.minDate = new Date("2009-08-09");
        this.maxDate = new Date();
        this.formErrors = {
            'walletName': '',
            'walletMnemonic': '',
            'walletDate': '',
            'walletPassword': '',
            'walletPassphrase': '',
        };
        this.validationMessages = {
            'walletName': {
                'required': 'A wallet name is required.',
                'minlength': 'A wallet name must be at least one character long.',
                'maxlength': 'A wallet name cannot be more than 24 characters long.',
                'pattern': 'Please enter a valid wallet name. [a-Z] and [0-9] are the only characters allowed.'
            },
            'walletMnemonic': {
                'required': 'Please enter your 12 word phrase.'
            },
            'walletDate': {
                'required': 'Please choose the date the wallet should sync from.'
            },
            'walletPassword': {
                'required': 'A password is required.'
            },
        };
        this.buildRecoverForm();
    }
    RecoverComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
        this.bsConfig = Object.assign({}, { showWeekNumbers: false, containerClass: 'theme-dark-blue' });
    };
    RecoverComponent.prototype.buildRecoverForm = function () {
        var _this = this;
        this.recoverWalletForm = this.fb.group({
            "walletName": ["", [
                    forms_1.Validators.required,
                    forms_1.Validators.minLength(1),
                    forms_1.Validators.maxLength(24),
                    forms_1.Validators.pattern(/^[a-zA-Z0-9]*$/)
                ]
            ],
            "walletMnemonic": ["", forms_1.Validators.required],
            "walletDate": ["", forms_1.Validators.required],
            "walletPassphrase": [""],
            "walletPassword": ["", forms_1.Validators.required],
            "selectNetwork": ["test", forms_1.Validators.required]
        });
        this.recoverWalletForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    RecoverComponent.prototype.onValueChanged = function (data) {
        if (!this.recoverWalletForm) {
            return;
        }
        var form = this.recoverWalletForm;
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
    RecoverComponent.prototype.onBackClicked = function () {
        this.router.navigate(["/setup"]);
    };
    RecoverComponent.prototype.onRecoverClicked = function () {
        this.isRecovering = true;
        var recoveryDate = new Date(this.recoverWalletForm.get("walletDate").value);
        recoveryDate.setDate(recoveryDate.getDate() - 1);
        this.walletRecovery = new wallet_recovery_1.WalletRecovery(this.recoverWalletForm.get("walletName").value, this.recoverWalletForm.get("walletMnemonic").value, this.recoverWalletForm.get("walletPassword").value, this.recoverWalletForm.get("walletPassphrase").value, recoveryDate);
        this.recoverWallet(this.walletRecovery);
    };
    RecoverComponent.prototype.recoverWallet = function (recoverWallet) {
        var _this = this;
        this.apiService.recoverStratisWallet(recoverWallet)
            .subscribe(function (response) {
            var body = "Your wallet has been recovered. \nYou will be redirected to the decryption page.";
            _this.genericModalService.openModal("Wallet Recovered", body);
            _this.router.navigate(['']);
        }, function (error) {
            _this.isRecovering = false;
        });
    };
    RecoverComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-recover',
            templateUrl: './recover.component.html',
            styleUrls: ['./recover.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, api_service_1.ApiService, modal_service_1.ModalService, router_1.Router, forms_1.FormBuilder])
    ], RecoverComponent);
    return RecoverComponent;
}());
exports.RecoverComponent = RecoverComponent;
//# sourceMappingURL=recover.component.js.map