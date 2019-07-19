"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var router_1 = require("@angular/router");
var global_service_1 = require("@shared/services/global.service");
var logout_confirmation_component_1 = require("../logout-confirmation/logout-confirmation.component");
var MenuComponent = /** @class */ (function () {
    function MenuComponent(modalService, globalService, router) {
        this.modalService = modalService;
        this.globalService = globalService;
        this.router = router;
        this.walletName = this.globalService.getWalletName();
    }
    MenuComponent.prototype.ngOnInit = function () {
        this.testnet = this.globalService.getTestnetEnabled();
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
    };
    MenuComponent.prototype.openAddressBook = function () {
        this.router.navigate(['/wallet/address-book']);
    };
    MenuComponent.prototype.openAdvanced = function () {
        this.router.navigate(['/wallet/advanced']);
    };
    MenuComponent.prototype.logoutClicked = function () {
        this.modalService.open(logout_confirmation_component_1.LogoutConfirmationComponent, { backdrop: "static" });
    };
    MenuComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-menu',
            templateUrl: './menu.component.html',
            styleUrls: ['./menu.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbModal, global_service_1.GlobalService, router_1.Router])
    ], MenuComponent);
    return MenuComponent;
}());
exports.MenuComponent = MenuComponent;
//# sourceMappingURL=menu.component.js.map