"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var LogoutConfirmationComponent = /** @class */ (function () {
    function LogoutConfirmationComponent(activeModal, router, apiService, genericModalService, globalService) {
        this.activeModal = activeModal;
        this.router = router;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.globalService = globalService;
    }
    LogoutConfirmationComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
    };
    LogoutConfirmationComponent.prototype.onLogout = function () {
        if (!this.sidechainEnabled) {
            this.apiService.stopStaking()
                .subscribe();
        }
        this.activeModal.close();
        this.router.navigate(['/login']);
    };
    LogoutConfirmationComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-logout-confirmation',
            templateUrl: './logout-confirmation.component.html',
            styleUrls: ['./logout-confirmation.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbActiveModal, router_1.Router, api_service_1.ApiService, modal_service_1.ModalService, global_service_1.GlobalService])
    ], LogoutConfirmationComponent);
    return LogoutConfirmationComponent;
}());
exports.LogoutConfirmationComponent = LogoutConfirmationComponent;
//# sourceMappingURL=logout-confirmation.component.js.map