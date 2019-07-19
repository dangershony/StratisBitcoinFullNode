"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var generic_modal_component_1 = require("../components/generic-modal/generic-modal.component");
var ModalService = /** @class */ (function () {
    function ModalService(modalService) {
        this.modalService = modalService;
    }
    ModalService.prototype.openModal = function (title, body) {
        var modalRef = this.modalService.open(generic_modal_component_1.GenericModalComponent, { backdrop: "static", keyboard: false });
        if (title) {
            modalRef.componentInstance.title = title;
        }
        if (body) {
            modalRef.componentInstance.body = body;
        }
    };
    ModalService = tslib_1.__decorate([
        core_1.Injectable({
            providedIn: 'root'
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbModal])
    ], ModalService);
    return ModalService;
}());
exports.ModalService = ModalService;
//# sourceMappingURL=modal.service.js.map