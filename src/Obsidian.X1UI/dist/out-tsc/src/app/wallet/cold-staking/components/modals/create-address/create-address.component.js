"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var ngx_clipboard_1 = require("ngx-clipboard");
var cold_staking_service_1 = require("../../../cold-staking.service");
var global_service_1 = require("@shared/services/global.service");
var ColdStakingCreateAddressComponent = /** @class */ (function () {
    function ColdStakingCreateAddressComponent(globalService, stakingService, activeModal, clipboardService) {
        this.globalService = globalService;
        this.stakingService = stakingService;
        this.activeModal = activeModal;
        this.clipboardService = clipboardService;
        this.address = '';
        this.addressCopied = false;
    }
    ColdStakingCreateAddressComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.stakingService.GetAddress(this.globalService.getWalletName()).subscribe(function (x) { return _this.address = x; });
    };
    ColdStakingCreateAddressComponent.prototype.closeClicked = function () {
        this.activeModal.close();
    };
    ColdStakingCreateAddressComponent.prototype.copyClicked = function () {
        if (this.address) {
            this.addressCopied = this.clipboardService.copyFromContent(this.address);
        }
    };
    ColdStakingCreateAddressComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-create-address',
            templateUrl: './create-address.component.html',
            styleUrls: ['./create-address.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, cold_staking_service_1.ColdStakingServiceBase,
            ng_bootstrap_1.NgbActiveModal, ngx_clipboard_1.ClipboardService])
    ], ColdStakingCreateAddressComponent);
    return ColdStakingCreateAddressComponent;
}());
exports.ColdStakingCreateAddressComponent = ColdStakingCreateAddressComponent;
//# sourceMappingURL=create-address.component.js.map