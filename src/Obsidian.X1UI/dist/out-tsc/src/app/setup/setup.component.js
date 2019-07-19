"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var global_service_1 = require("@shared/services/global.service");
var SetupComponent = /** @class */ (function () {
    function SetupComponent(router, globalService) {
        this.router = router;
        this.globalService = globalService;
    }
    SetupComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
    };
    SetupComponent.prototype.onCreateClicked = function () {
        this.router.navigate(['setup/create']);
    };
    SetupComponent.prototype.onRecoverClicked = function () {
        this.router.navigate(['setup/recover']);
    };
    SetupComponent.prototype.onBackClicked = function () {
        this.router.navigate(['login']);
    };
    SetupComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'setup-component',
            templateUrl: './setup.component.html',
            styleUrls: ['./setup.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [router_1.Router, global_service_1.GlobalService])
    ], SetupComponent);
    return SetupComponent;
}());
exports.SetupComponent = SetupComponent;
//# sourceMappingURL=setup.component.js.map