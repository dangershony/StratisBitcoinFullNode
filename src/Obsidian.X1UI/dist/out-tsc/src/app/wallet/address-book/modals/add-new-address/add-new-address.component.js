"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var forms_1 = require("@angular/forms");
var api_service_1 = require("@shared/services/api.service");
var modal_service_1 = require("@shared/services/modal.service");
var address_label_1 = require("@shared/models/address-label");
var AddNewAddressComponent = /** @class */ (function () {
    function AddNewAddressComponent(activeModel, apiService, genericModalService, fb) {
        this.activeModel = activeModel;
        this.apiService = apiService;
        this.genericModalService = genericModalService;
        this.fb = fb;
        this.formErrors = {
            'label': '',
            'address': ''
        };
        this.validationMessages = {
            'label': {
                'required': 'Please enter a label for your address.',
                'minlength': 'A label needs to be at least 2 characters long.',
                'maxlength': "A label can't be more than 40 characters long."
            },
            'address': {
                'required': 'Please add a valid address.'
            }
        };
        this.buildAddressForm();
    }
    AddNewAddressComponent.prototype.buildAddressForm = function () {
        var _this = this;
        this.addressForm = this.fb.group({
            "label": ["", forms_1.Validators.compose([forms_1.Validators.required, forms_1.Validators.minLength(2), forms_1.Validators.maxLength(40)])],
            "address": ["", forms_1.Validators.required],
        });
        this.addressForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
    };
    AddNewAddressComponent.prototype.onValueChanged = function (data) {
        if (!this.addressForm) {
            return;
        }
        var form = this.addressForm;
        for (var field in this.formErrors) {
            this.formErrors[field] = '';
            var control = form.get(field);
            if (control && control.dirty && !control.valid) {
                var messages = this.validationMessages[field];
                for (var key in control.errors) {
                    this.formErrors[field] += messages[key] + ' ';
                }
            }
        }
    };
    AddNewAddressComponent.prototype.createClicked = function () {
        var _this = this;
        var addressLabel = new address_label_1.AddressLabel(this.addressForm.get("label").value, this.addressForm.get("address").value);
        this.apiService.addAddressBookAddress(addressLabel)
            .subscribe(function (response) {
            _this.activeModel.close();
        });
    };
    AddNewAddressComponent.prototype.closeClicked = function () {
        this.activeModel.close();
    };
    AddNewAddressComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-add-new-address',
            templateUrl: './add-new-address.component.html',
            styleUrls: ['./add-new-address.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [ng_bootstrap_1.NgbActiveModal, api_service_1.ApiService, modal_service_1.ModalService, forms_1.FormBuilder])
    ], AddNewAddressComponent);
    return AddNewAddressComponent;
}());
exports.AddNewAddressComponent = AddNewAddressComponent;
//# sourceMappingURL=add-new-address.component.js.map