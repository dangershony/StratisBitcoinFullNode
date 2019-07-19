"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var GenericModalComponent = /** @class */ (function () {
    function GenericModalComponent(activeModal) {
        this.activeModal = activeModal;
        this.title = "Something went wrong";
        this.body = "Something went wrong while connecting to the API. Please restart the application.";
    }
    GenericModalComponent.prototype.ngOnInit = function () {
    };
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String)
    ], GenericModalComponent.prototype, "title", void 0);
    tslib_1.__decorate([
        core_1.Input(),
        tslib_1.__metadata("design:type", String)
    ], GenericModalComponent.prototype, "body", void 0);
    GenericModalComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-generic-modal',
            templateUrl: './generic-modal.component.html',
            styleUrls: ['./generic-modal.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbActiveModal])
    ], GenericModalComponent);
    return GenericModalComponent;
}());
exports.GenericModalComponent = GenericModalComponent;
//# sourceMappingURL=generic-modal.component.js.map