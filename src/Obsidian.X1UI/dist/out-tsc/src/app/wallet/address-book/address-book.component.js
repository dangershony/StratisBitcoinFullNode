"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ngx_clipboard_1 = require("ngx-clipboard");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var send_component_1 = require("../send/send.component");
var add_new_address_component_1 = require("../address-book/modals/add-new-address/add-new-address.component");
var address_label_1 = require("@shared/models/address-label");
var AddressBookComponent = /** @class */ (function () {
    function AddressBookComponent(apiService, clipboardService, modalService, genericModalService) {
        this.apiService = apiService;
        this.clipboardService = clipboardService;
        this.modalService = modalService;
        this.genericModalService = genericModalService;
    }
    AddressBookComponent.prototype.ngOnInit = function () {
        this.startSubscriptions();
    };
    AddressBookComponent.prototype.ngOnDestroy = function () {
        this.cancelSubscriptions();
    };
    AddressBookComponent.prototype.startSubscriptions = function () {
        this.getAddressBookAddresses();
    };
    AddressBookComponent.prototype.cancelSubscriptions = function () {
        if (this.addressBookSubcription) {
            this.addressBookSubcription.unsubscribe();
        }
    };
    AddressBookComponent.prototype.getAddressBookAddresses = function () {
        var _this = this;
        this.addressBookSubcription = this.apiService.getAddressBookAddresses()
            .subscribe(function (response) {
            _this.addresses = null;
            if (response.addresses[0]) {
                _this.addresses = [];
                var addressResponse = response.addresses;
                for (var _i = 0, addressResponse_1 = addressResponse; _i < addressResponse_1.length; _i++) {
                    var address = addressResponse_1[_i];
                    _this.addresses.push(new address_label_1.AddressLabel(address.label, address.address));
                }
            }
        }, function (error) {
            if (error.status === 0) {
                _this.cancelSubscriptions();
            }
            else if (error.status >= 400) {
                if (!error.error.errors[0].message) {
                    _this.cancelSubscriptions();
                    _this.startSubscriptions();
                }
            }
        });
    };
    AddressBookComponent.prototype.copyToClipboardClicked = function (address) {
        if (this.clipboardService.copyFromContent(address.address)) {
        }
    };
    AddressBookComponent.prototype.sendClicked = function (address) {
        var modalRef = this.modalService.open(send_component_1.SendComponent, { backdrop: "static" });
        modalRef.componentInstance.address = address.address;
    };
    AddressBookComponent.prototype.removeClicked = function (address) {
        var _this = this;
        this.apiService.removeAddressBookAddress(address.label)
            .subscribe(function (response) {
            _this.cancelSubscriptions();
            _this.startSubscriptions();
        });
    };
    AddressBookComponent.prototype.addNewAddressClicked = function () {
        this.modalService.open(add_new_address_component_1.AddNewAddressComponent, { backdrop: "static" });
    };
    AddressBookComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-address-book',
            templateUrl: './address-book.component.html',
            styleUrls: ['./address-book.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, ngx_clipboard_1.ClipboardService, ng_bootstrap_1.NgbModal, modal_service_1.ModalService])
    ], AddressBookComponent);
    return AddressBookComponent;
}());
exports.AddressBookComponent = AddressBookComponent;
//# sourceMappingURL=address-book.component.js.map