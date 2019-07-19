"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var router_1 = require("@angular/router");
var global_service_1 = require("@shared/services/global.service");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var password_validation_directive_1 = require("@shared/directives/password-validation.directive");
var wallet_creation_1 = require("@shared/models/wallet-creation");
var CreateComponent = /** @class */ (function () {
    function CreateComponent(globalService, apiService, genericModalService, router, fb) {
        this.globalService = globalService;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.router = router;
        this.fb = fb;
        this.hostValidator = forms_1.Validators.compose([
            forms_1.Validators.required,
            forms_1.Validators.pattern(/^([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})$/)
        ]);
        this.passphraseValidator = forms_1.Validators.compose([
            forms_1.Validators.required,
            forms_1.Validators.minLength(12),
            forms_1.Validators.maxLength(60),
            forms_1.Validators.pattern(/^[a-zA-Z0-9]*$/)
        ]);
        this.nameValidator = forms_1.Validators.compose([
            forms_1.Validators.required,
            forms_1.Validators.minLength(1),
            forms_1.Validators.maxLength(24),
            forms_1.Validators.pattern(/^[a-zA-Z0-9]*$/)
        ]);
        this.formErrors = {
            'host': 'Invalid host name.',
            'walletName': '',
            'walletPassword': '',
            'walletPasswordConfirmation': ''
        };
        this.validationMessages = {
            'host': {
                'required': "Please enter a host, e.g. 'localhost'",
                'pattern': "Invalid IP address."
            },
            'walletName': {
                'required': 'A wallet name is required.',
                'minlength': 'A wallet name must be at least one character long.',
                'maxlength': 'A wallet name cannot be more than 24 characters long.',
                'pattern': 'Please enter a valid wallet name. [a-Z] and [0-9] are the only characters allowed.'
            },
            'walletPassword': {
                'required': 'A passphrase is required',
                'pattern': 'A passphrase must be from 12 to 60 characters long and contain only lowercase and uppercase latin characters and numbers.'
            },
            'walletPasswordConfirmation': {
                'required': 'Confirm your passphrase.',
                'walletPasswordConfirmation': 'Passphrases do not match.'
            }
        };
        this.buildCreateForm();
    }
    CreateComponent.prototype.ngOnInit = function () {
    };
    CreateComponent.prototype.buildCreateForm = function () {
        var _this = this;
        this.createWalletForm = this.fb.group({
            "host": ["127.0.0.1", this.hostValidator],
            "walletName": ["", this.nameValidator],
            "walletPassword": ["", this.passphraseValidator],
            "walletPasswordConfirmation": ["", forms_1.Validators.required],
            "selectNetwork": ["test", forms_1.Validators.required]
        }, {
            validator: password_validation_directive_1.PasswordValidationDirective.MatchPassword
        });
        this.createWalletForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    CreateComponent.prototype.onValueChanged = function (data) {
        if (!this.createWalletForm) {
            return;
        }
        var form = this.createWalletForm;
        for (var field in this.formErrors) {
            this.formErrors[field] = '';
            var control = form.get(field);
            if (control && control.dirty && !control.valid) {
                var messages = this.validationMessages[field];
                for (var key in control.errors) {
                    this.formErrors[field] += messages[key] + ' ';
                }
            }
            if (control && field === "host") {
                if (control.valid) {
                    console.log("Setting host to " + control.value);
                    this.globalService.setDaemonIP(control.value);
                }
                else {
                }
            }
        }
    };
    CreateComponent.prototype.onBackClicked = function () {
        this.router.navigate(["/setup"]);
    };
    CreateComponent.prototype.onCreateClicked = function () {
        this.newWallet = new wallet_creation_1.WalletCreation(this.createWalletForm.get("walletName").value, "", this.createWalletForm.get("walletPassword").value, "");
        this.router.navigate(['/setup/create/show-mnemonic'], { queryParams: { name: this.newWallet.name, mnemonic: this.newWallet.mnemonic, password: this.newWallet.password, passphrase: this.newWallet.passphrase } });
    };
    CreateComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'create-component',
            templateUrl: './create.component.html',
            styleUrls: ['./create.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, api_service_1.ApiService, modal_service_1.ModalService, router_1.Router, forms_1.FormBuilder])
    ], CreateComponent);
    return CreateComponent;
}());
exports.CreateComponent = CreateComponent;
//# sourceMappingURL=create.component.js.map