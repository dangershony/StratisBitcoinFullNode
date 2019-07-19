"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var wallet_creation_1 = require("@shared/models/wallet-creation");
var global_service_1 = require("@shared/services/global.service");
var ShowMnemonicComponent = /** @class */ (function () {
    function ShowMnemonicComponent(route, router, globalService) {
        this.route = route;
        this.router = router;
        this.globalService = globalService;
    }
    ShowMnemonicComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
        this.subscription = this.route.queryParams.subscribe(function (params) {
            _this.newWallet = new wallet_creation_1.WalletCreation(params["name"], params["mnemonic"], params["password"], params["passphrase"]);
        });
        this.showMnemonic();
    };
    ShowMnemonicComponent.prototype.showMnemonic = function () {
        this.mnemonic = this.newWallet.mnemonic;
        this.mnemonicArray = this.mnemonic.split(" ");
    };
    ShowMnemonicComponent.prototype.onContinueClicked = function () {
        this.router.navigate(['/setup/create/confirm-mnemonic'], { queryParams: { name: this.newWallet.name, mnemonic: this.newWallet.mnemonic, password: this.newWallet.password, passphrase: this.newWallet.passphrase } });
    };
    ShowMnemonicComponent.prototype.onCancelClicked = function () {
        this.router.navigate(['']);
    };
    ShowMnemonicComponent.prototype.ngOnDestroy = function () {
        this.subscription.unsubscribe();
    };
    ShowMnemonicComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-show-mnemonic',
            templateUrl: './show-mnemonic.component.html',
            styleUrls: ['./show-mnemonic.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [router_1.ActivatedRoute, router_1.Router, global_service_1.GlobalService])
    ], ShowMnemonicComponent);
    return ShowMnemonicComponent;
}());
exports.ShowMnemonicComponent = ShowMnemonicComponent;
//# sourceMappingURL=show-mnemonic.component.js.map