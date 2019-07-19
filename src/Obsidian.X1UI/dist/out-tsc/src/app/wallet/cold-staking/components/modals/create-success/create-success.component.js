"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var ColdStakingCreateSuccessComponent = /** @class */ (function () {
    function ColdStakingCreateSuccessComponent(activeModal) {
        this.activeModal = activeModal;
    }
    ColdStakingCreateSuccessComponent.prototype.okClicked = function () {
        this.activeModal.close();
    };
    ColdStakingCreateSuccessComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-create-success',
            templateUrl: './create-success.component.html',
            styleUrls: ['./create-success.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbActiveModal])
    ], ColdStakingCreateSuccessComponent);
    return ColdStakingCreateSuccessComponent;
}());
exports.ColdStakingCreateSuccessComponent = ColdStakingCreateSuccessComponent;
//# sourceMappingURL=create-success.component.js.map