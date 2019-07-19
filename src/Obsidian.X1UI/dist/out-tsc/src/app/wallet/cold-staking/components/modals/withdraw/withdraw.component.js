"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var global_service_1 = require("@shared/services/global.service");
var cold_staking_service_1 = require("../../../cold-staking.service");
var ColdStakingWithdrawComponent = /** @class */ (function () {
    function ColdStakingWithdrawComponent(globalService, stakingService, activeModal) {
        var _this = this;
        this.globalService = globalService;
        this.stakingService = stakingService;
        this.activeModal = activeModal;
        this._amountFormatted = '';
        this._amountSpendable = 0;
        this._destinationAddress = '';
        this._password = '';
        this.amountSpendableFormatted = '';
        this.passwordValid = false;
        this.canWithdraw = false;
        this.feeTypes = [
            { id: 0, display: 'Low - 0.0001 STRAT' },
            { id: 1, display: 'Medium - 0.001 STRAT' },
            { id: 2, display: 'High - 0.01 STRAT' },
        ];
        this.closeClicked = function () { return _this.activeModal.close(); };
        this.selectedFeeType = this.feeTypes[1];
    }
    Object.defineProperty(ColdStakingWithdrawComponent.prototype, "amount", {
        get: function () {
            return this._amount;
        },
        set: function (value) {
            this._amount = value;
            this._amountFormatted = this._amount.toString();
            this.setCanWithdraw();
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingWithdrawComponent.prototype, "destinationAddress", {
        get: function () {
            return this._destinationAddress;
        },
        set: function (value) {
            this._destinationAddress = value;
            this.setCanWithdraw();
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingWithdrawComponent.prototype, "password", {
        get: function () {
            return this._password;
        },
        set: function (value) {
            this._password = value;
            this.passwordValid = this._password.length > 0;
            this.setCanWithdraw();
        },
        enumerable: true,
        configurable: true
    });
    ColdStakingWithdrawComponent.prototype.setCanWithdraw = function () {
        this.canWithdraw = this._amountFormatted.length && this._destinationAddress.length && this.passwordValid;
    };
    ColdStakingWithdrawComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.setCanWithdraw();
        this.stakingService.GetInfo(this.globalService.getWalletName()).subscribe(function (x) {
            _this._amountSpendable = x.coldWalletAmount;
            _this.amountSpendableFormatted = _this._amountSpendable.toLocaleString();
        });
    };
    ColdStakingWithdrawComponent.prototype.withdrawClicked = function () {
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Number),
        tslib_1.__metadata("design:paramtypes", [Number])
    ], ColdStakingWithdrawComponent.prototype, "amount", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String),
        tslib_1.__metadata("design:paramtypes", [String])
    ], ColdStakingWithdrawComponent.prototype, "destinationAddress", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String),
        tslib_1.__metadata("design:paramtypes", [String])
    ], ColdStakingWithdrawComponent.prototype, "password", null);
    ColdStakingWithdrawComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-withdraw',
            templateUrl: './withdraw.component.html',
            styleUrls: ['./withdraw.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, cold_staking_service_1.ColdStakingServiceBase, ng_bootstrap_1.NgbActiveModal])
    ], ColdStakingWithdrawComponent);
    return ColdStakingWithdrawComponent;
}());
exports.ColdStakingWithdrawComponent = ColdStakingWithdrawComponent;
//# sourceMappingURL=withdraw.component.js.map