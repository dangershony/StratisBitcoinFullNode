"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var router_1 = require("@angular/router");
var global_service_1 = require("@shared/services/global.service");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_load_1 = require("@shared/models/wallet-load");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
window.clientKeyPair = window.clientKeyPair || {};
// end extend window for vcl
var LoginComponent = /** @class */ (function () {
    function LoginComponent(globalService, apiService, genericModalService, router, fb) {
        this.globalService = globalService;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.router = router;
        this.fb = fb;
        this.hasWallet = true;
        this.isDecrypting = false;
        this.authKey = api_service_1.ApiService.serverAuthKeyHex;
        this.hostValidator = forms_1.Validators.compose([
            forms_1.Validators.required,
            forms_1.Validators.minLength(1),
            forms_1.Validators.maxLength(24),
            forms_1.Validators.pattern(/^([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})[.]([0-9]{1,3})$/)
        ]);
        this.formErrors = {
            'host': 'Invalid host name.'
        };
        this.validationMessages = {
            'host': {
                'required': "Please enter a host, e.g. 'localhost'",
                'pattern': "Invalid IP address."
            }
        };
        this.buildDecryptForm();
    }
    LoginComponent.prototype.ngOnInit = function () {
        this.getWalletFiles();
    };
    LoginComponent.prototype.buildDecryptForm = function () {
        var _this = this;
        this.openWalletForm = this.fb.group({
            "selectWallet": [{ value: "", disabled: this.isDecrypting }, forms_1.Validators.required],
            "host": [{ value: "127.0.0.1", disabled: this.isDecrypting }, this.hostValidator]
        });
        this.openWalletForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    LoginComponent.prototype.onValueChanged = function (data) {
        if (!this.openWalletForm) {
            return;
        }
        var form = this.openWalletForm;
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
                this.wallets = [];
                if (control.valid) {
                    console.log("Setting host to " + control.value);
                    this.globalService.setDaemonIP(control.value);
                    this.getWalletFiles();
                }
                else {
                }
            }
        }
    };
    LoginComponent.prototype.getWalletFiles = function () {
        if (!api_service_1.ApiService.serverPublicKey) {
            setTimeout(this.getWalletFiles.bind(this), 1000);
            return;
        }
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("getWalletFiles", ""), this.onGetWalletFiles.bind(this));
    };
    LoginComponent.prototype.onGetWalletFiles = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.wallets = responsePayload.responsePayload.walletsFiles;
            this.globalService.setWalletPath(responsePayload.responsePayload.walletsPath);
            if (this.wallets.length > 0) {
                for (var wallet in this.wallets) {
                    this.wallets[wallet] = this.wallets[wallet].slice(0, -12);
                }
            }
            else {
            }
        }
        else {
            if (responsePayload.status === 401) {
                this.info = "User name and password are required for this node.";
            }
            setTimeout(this.getWalletFiles.bind(this), 1000);
        }
    };
    LoginComponent.prototype.loadWallet = function (walletLoad) {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("loadWallet", walletLoad), this.onLoadWallet.bind(this));
    };
    LoginComponent.prototype.onLoadWallet = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.router.navigate(['wallet/dashboard']);
        }
        else {
            this.isDecrypting = false;
        }
    };
    LoginComponent.prototype.onCreateClicked = function () {
        this.router.navigate(['setup']);
    };
    LoginComponent.prototype.onEnter = function () {
        if (this.openWalletForm.valid) {
            this.onDecryptClicked();
        }
    };
    LoginComponent.prototype.onDecryptClicked = function () {
        this.isDecrypting = true;
        var walletName = this.openWalletForm.get("selectWallet").value;
        this.globalService.setWalletName(walletName);
        var walletLoad = new wallet_load_1.WalletLoad(walletName, "pw_check_in_login.component_is_not_supported"
        //this.openWalletForm.get("password").value
        );
        this.loadWallet(walletLoad);
    };
    LoginComponent.prototype.getNodeStatus = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("nodeStatus", ""), this.onGetNodeStatus.bind(this));
    };
    LoginComponent.prototype.onGetNodeStatus = function (responsePayload) {
        if (responsePayload.status === 200) {
            var nodeStatus = responsePayload.responsePayload;
            this.globalService.setCoinUnit(nodeStatus.coinTicker);
            this.globalService.setNetwork(nodeStatus.network);
        }
    };
    LoginComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-login',
            templateUrl: './login.component.html',
            styleUrls: ['./login.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, api_service_1.ApiService, modal_service_1.ModalService, router_1.Router, forms_1.FormBuilder])
    ], LoginComponent);
    return LoginComponent;
}());
exports.LoginComponent = LoginComponent;
//# sourceMappingURL=login.component.js.map