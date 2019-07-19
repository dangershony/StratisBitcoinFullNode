"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var ReceiveComponent = /** @class */ (function () {
    function ReceiveComponent(apiService, globalService, activeModal, genericModalService) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.activeModal = activeModal;
        this.genericModalService = genericModalService;
        this.address = "";
        this.copied = false;
        this.showAll = false;
        this.pageNumberUsed = 1;
        this.pageNumberUnused = 1;
        this.pageNumberChange = 1;
    }
    ReceiveComponent.prototype.ngOnInit = function () {
        this.sidechainEnabled = this.globalService.getSidechainEnabled();
        this.getReceiveAddresses();
        this.showAllAddresses();
    };
    ReceiveComponent.prototype.onCopiedClick = function () {
        this.copied = true;
    };
    ReceiveComponent.prototype.showAllAddresses = function () {
        this.showAll = true;
    };
    ReceiveComponent.prototype.showOneAddress = function () {
        this.showAll = false;
    };
    ReceiveComponent.prototype.getReceiveAddresses = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("getReceiveAddresses", ""), this.onGetReceiveAddresses.bind(this));
    };
    ReceiveComponent.prototype.onGetReceiveAddresses = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.allAddresses = [];
            this.usedAddresses = [];
            this.unusedAddresses = [];
            this.changeAddresses = [];
            this.allAddresses = responsePayload.responsePayload.addresses;
            for (var _i = 0, _a = this.allAddresses; _i < _a.length; _i++) {
                var address = _a[_i];
                if (address.isUsed) {
                    this.usedAddresses.push(address.address);
                }
                else if (address.isChange) {
                    this.changeAddresses.push(address.address);
                }
                else {
                    this.unusedAddresses.push(address.address);
                }
            }
        }
    };
    ReceiveComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'receive-component',
            templateUrl: './receive.component.html',
            styleUrls: ['./receive.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, ng_bootstrap_1.NgbActiveModal, modal_service_1.ModalService])
    ], ReceiveComponent);
    return ReceiveComponent;
}());
exports.ReceiveComponent = ReceiveComponent;
//# sourceMappingURL=receive.component.js.map