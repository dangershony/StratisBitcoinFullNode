"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var router_1 = require("@angular/router");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_creation_1 = require("@shared/models/wallet-creation");
var secret_word_index_generator_1 = require("./secret-word-index-generator");
var global_service_1 = require("@shared/services/global.service");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var ConfirmMnemonicComponent = /** @class */ (function () {
    function ConfirmMnemonicComponent(apiService, genericModalService, route, router, fb, globalService) {
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.route = route;
        this.router = router;
        this.fb = fb;
        this.globalService = globalService;
        this.secretWordIndexGenerator = new secret_word_index_generator_1.SecretWordIndexGenerator();
        this.matchError = "";
        this.passphraseValidator = forms_1.Validators.compose([
            forms_1.Validators.required,
            forms_1.Validators.minLength(12),
            forms_1.Validators.maxLength(60),
            forms_1.Validators.pattern(/^[a-zA-Z0-9]*$/)
        ]);
        this.formErrors = {
            'word1': '',
            'word2': '',
            'word3': ''
        };
        this.validationMessages = {
            'word1': {
                'required': 'This secret word is required.',
                'minlength': 'The passphrase must be at least one character long',
                'maxlength': 'The passphrase can not be longer than 60 characters',
                'pattern': 'Only latin uppercase and lowercase characters and numbers allowed.'
            }
        };
        this.buildMnemonicForm();
    }
    ConfirmMnemonicComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.subscription = this.route.queryParams.subscribe(function (params) {
            _this.newWallet = new wallet_creation_1.WalletCreation(params["name"], params["mnemonic"], params["password"], params["passphrase"]);
        });
    };
    ConfirmMnemonicComponent.prototype.buildMnemonicForm = function () {
        var _this = this;
        this.mnemonicForm = this.fb.group({
            "word1": ["", this.passphraseValidator]
        });
        this.mnemonicForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    ConfirmMnemonicComponent.prototype.onValueChanged = function (data) {
        if (!this.mnemonicForm) {
            return;
        }
        var form = this.mnemonicForm;
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
        this.matchError = "";
    };
    ConfirmMnemonicComponent.prototype.onConfirmClicked = function () {
        this.checkMnemonic();
        if (this.checkMnemonic()) {
            this.isCreating = true;
            this.createWallet(this.newWallet);
        }
    };
    ConfirmMnemonicComponent.prototype.onBackClicked = function () {
        this.router.navigate(['/setup/create/show-mnemonic'], { queryParams: { name: this.newWallet.name, mnemonic: this.newWallet.mnemonic, password: this.newWallet.password, passphrase: this.newWallet.passphrase } });
    };
    ConfirmMnemonicComponent.prototype.checkMnemonic = function () {
        if (this.mnemonicForm.get('word1').value.trim() === this.newWallet.password) {
            return true;
        }
        else {
            this.matchError = 'The passphrase is not correct.';
            return false;
        }
    };
    ConfirmMnemonicComponent.prototype.createWallet = function (wallet) {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("createWallet", wallet), this.onCreateWallet.bind(this));
    };
    ConfirmMnemonicComponent.prototype.onCreateWallet = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.genericModalService.openModal("Wallet Created", "Your wallet has been created.<br>Keep passphrase safe and <b>make a backup of your wallet<b>!");
            this.router.navigate(['']);
        }
        else {
            this.isCreating = false;
            this.matchError = responsePayload.statusText;
        }
    };
    ConfirmMnemonicComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-confirm-mnemonic',
            templateUrl: './confirm-mnemonic.component.html',
            styleUrls: ['./confirm-mnemonic.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, modal_service_1.ModalService, router_1.ActivatedRoute, router_1.Router, forms_1.FormBuilder, global_service_1.GlobalService])
    ], ConfirmMnemonicComponent);
    return ConfirmMnemonicComponent;
}());
exports.ConfirmMnemonicComponent = ConfirmMnemonicComponent;
//# sourceMappingURL=confirm-mnemonic.component.js.map