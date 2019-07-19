"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ColdStakingWalletComponent = /** @class */ (function () {
    function ColdStakingWalletComponent() {
        var _this = this;
        this._hotWallet = false;
        this._balance = 0;
        this._amount = 0;
        this.balanceFormatted = '';
        this.amountFormatted = '';
        this.description = '';
        this.onGetFirstUnusedAddress = new core_1.EventEmitter();
        this.onWithdraw = new core_1.EventEmitter();
        this.unusedAddressClicked = function () { return _this.onGetFirstUnusedAddress.emit(_this); };
        this.withdrawClicked = function () { return _this.onWithdraw.emit(_this); };
    }
    Object.defineProperty(ColdStakingWalletComponent.prototype, "hotWallet", {
        get: function () {
            return this._hotWallet;
        },
        set: function (value) {
            this._hotWallet = value;
            this.description = value ? 'Coins that you can only stake but other wallets can spend' : 'Coins blah to be defined';
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingWalletComponent.prototype, "balance", {
        set: function (value) {
            this._balance = value;
            this.balanceFormatted = this._balance.toLocaleString();
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingWalletComponent.prototype, "amount", {
        set: function (value) {
            this._amount = value;
            this.amountFormatted = this._amount.toLocaleString();
        },
        enumerable: true,
        configurable: true
    });
    tslib_1.__decorate([
        core_1.Output(),
        tslib_1.__metadata("design:type", Object)
    ], ColdStakingWalletComponent.prototype, "onGetFirstUnusedAddress", void 0);
    tslib_1.__decorate([
        core_1.Output(),
        tslib_1.__metadata("design:type", Object)
    ], ColdStakingWalletComponent.prototype, "onWithdraw", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Boolean),
        tslib_1.__metadata("design:paramtypes", [Boolean])
    ], ColdStakingWalletComponent.prototype, "hotWallet", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Number),
        tslib_1.__metadata("design:paramtypes", [Number])
    ], ColdStakingWalletComponent.prototype, "balance", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Number),
        tslib_1.__metadata("design:paramtypes", [Number])
    ], ColdStakingWalletComponent.prototype, "amount", null);
    ColdStakingWalletComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-staking-wallet',
            templateUrl: './wallet.component.html',
            styleUrls: ['./wallet.component.css']
        })
    ], ColdStakingWalletComponent);
    return ColdStakingWalletComponent;
}());
exports.ColdStakingWalletComponent = ColdStakingWalletComponent;
//# sourceMappingURL=wallet.component.js.map