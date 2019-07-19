"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var global_service_1 = require("@shared/services/global.service");
var cold_staking_service_1 = require("../../cold-staking.service");
var create_address_component_1 = require("../modals/create-address/create-address.component");
var withdraw_component_1 = require("../modals/withdraw/withdraw.component");
var create_component_1 = require("../modals/create/create.component");
var ColdStakingOverviewComponent = /** @class */ (function () {
    function ColdStakingOverviewComponent(globalService, stakingService, modalService) {
        this.globalService = globalService;
        this.stakingService = stakingService;
        this.modalService = modalService;
    }
    ColdStakingOverviewComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.stakingService.GetInfo(this.globalService.getWalletName()).subscribe(function (x) { return _this.stakingInfo = x; });
    };
    ColdStakingOverviewComponent.prototype.onWalletGetFirstUnusedAddress = function (walletComponent) {
        this.modalService.open(create_address_component_1.ColdStakingCreateAddressComponent);
    };
    ColdStakingOverviewComponent.prototype.onWalletWithdraw = function (walletComponent) {
        this.modalService.open(withdraw_component_1.ColdStakingWithdrawComponent);
    };
    ColdStakingOverviewComponent.prototype.onSetup = function () {
        this.modalService.open(create_component_1.ColdStakingCreateComponent);
    };
    ColdStakingOverviewComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-staking-scene',
            templateUrl: './overview.component.html',
            styleUrls: ['./overview.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, cold_staking_service_1.ColdStakingServiceBase, ng_bootstrap_1.NgbModal])
    ], ColdStakingOverviewComponent);
    return ColdStakingOverviewComponent;
}());
exports.ColdStakingOverviewComponent = ColdStakingOverviewComponent;
//# sourceMappingURL=overview.component.js.map