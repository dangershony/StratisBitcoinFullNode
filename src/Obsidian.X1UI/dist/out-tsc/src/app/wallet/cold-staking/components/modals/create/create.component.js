"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var global_service_1 = require("@shared/services/global.service");
var cold_staking_service_1 = require("../../../cold-staking.service");
var create_success_component_1 = require("../create-success/create-success.component");
var router_1 = require("@angular/router");
var HotColdWallet;
(function (HotColdWallet) {
    HotColdWallet[HotColdWallet["Hot"] = 1] = "Hot";
    HotColdWallet[HotColdWallet["Cold"] = 2] = "Cold";
})(HotColdWallet || (HotColdWallet = {}));
;
var ColdStakingCreateComponent = /** @class */ (function () {
    function ColdStakingCreateComponent(globalService, stakingService, activeModal, modalService, routerService) {
        var _this = this;
        this.globalService = globalService;
        this.stakingService = stakingService;
        this.activeModal = activeModal;
        this.modalService = modalService;
        this.routerService = routerService;
        this._amountFormatted = '';
        this._destinationAddress = '';
        this._password = '';
        this.passwordValid = false;
        this.canCreate = false;
        this.opacity = 1;
        this.feeTypes = [
            { id: 0, display: 'Low - 0.0001 STRAT' },
            { id: 1, display: 'Medium - 0.001 STRAT' },
            { id: 2, display: 'High - 0.01 STRAT' },
        ];
        this.HotColdWalletEnum = HotColdWallet;
        this.hotColdWalletSelection = HotColdWallet.Hot;
        this.closeClicked = function () { return _this.activeModal.close(); };
        this.selectedFeeType = this.feeTypes[1];
        this.setCanCreate();
    }
    Object.defineProperty(ColdStakingCreateComponent.prototype, "amount", {
        get: function () {
            return this._amount;
        },
        set: function (value) {
            this._amount = value;
            this._amountFormatted = this._amount.toString();
            this.setCanCreate();
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingCreateComponent.prototype, "destinationAddress", {
        get: function () {
            return this._destinationAddress;
        },
        set: function (value) {
            this._destinationAddress = value;
            this.setCanCreate();
        },
        enumerable: true,
        configurable: true
    });
    Object.defineProperty(ColdStakingCreateComponent.prototype, "password", {
        get: function () {
            return this._password;
        },
        set: function (value) {
            this._password = value;
            this.passwordValid = this._password.length > 0;
            this.setCanCreate();
        },
        enumerable: true,
        configurable: true
    });
    ColdStakingCreateComponent.prototype.setCanCreate = function () {
        this.canCreate = this._amountFormatted.length && this._destinationAddress.length && this.passwordValid;
    };
    ColdStakingCreateComponent.prototype.createClicked = function () {
        var _this = this;
        this.stakingService.CreateColdstaking(this.globalService.getWalletName())
            .subscribe(function (success) {
            if (success) {
                _this.opacity = .5;
                _this.modalService.open(create_success_component_1.ColdStakingCreateSuccessComponent, { backdrop: 'static' }).result
                    .then(function (_) { return _this.activeModal.close(); });
            }
        });
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", Number),
        tslib_1.__metadata("design:paramtypes", [Number])
    ], ColdStakingCreateComponent.prototype, "amount", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String),
        tslib_1.__metadata("design:paramtypes", [String])
    ], ColdStakingCreateComponent.prototype, "destinationAddress", null);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String),
        tslib_1.__metadata("design:paramtypes", [String])
    ], ColdStakingCreateComponent.prototype, "password", null);
    ColdStakingCreateComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-create',
            templateUrl: './create.component.html',
            styleUrls: ['./create.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, cold_staking_service_1.ColdStakingServiceBase,
            ng_bootstrap_1.NgbActiveModal, ng_bootstrap_1.NgbModal, router_1.Router])
    ], ColdStakingCreateComponent);
    return ColdStakingCreateComponent;
}());
exports.ColdStakingCreateComponent = ColdStakingCreateComponent;
//# sourceMappingURL=create.component.js.map