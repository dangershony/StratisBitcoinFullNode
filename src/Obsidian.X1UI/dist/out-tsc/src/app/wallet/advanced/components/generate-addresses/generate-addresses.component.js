"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var forms_1 = require("@angular/forms");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var wallet_info_1 = require("@shared/models/wallet-info");
var GenerateAddressesComponent = /** @class */ (function () {
    function GenerateAddressesComponent(apiService, globalService, genericModalService, fb) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.genericModalService = genericModalService;
        this.fb = fb;
        this.pageNumber = 1;
        this.formErrors = {
            'generateAddresses': ''
        };
        this.validationMessages = {
            'generateAddresses': {
                'required': 'Please enter an amount to generate.',
                'pattern': 'Please enter a number between 1 and 10.',
                'min': 'Please generate at least one address.',
                'max': 'You can only generate 1000 addresses at once.'
            }
        };
        this.buildGenerateAddressesForm();
    }
    GenerateAddressesComponent.prototype.ngOnInit = function () {
    };
    GenerateAddressesComponent.prototype.buildGenerateAddressesForm = function () {
        var _this = this;
        this.generateAddressesForm = this.fb.group({
            "generateAddresses": ["", forms_1.Validators.compose([forms_1.Validators.required, forms_1.Validators.pattern("^[0-9]*$"), forms_1.Validators.min(1), forms_1.Validators.max(1000)])]
        });
        this.generateAddressesForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    GenerateAddressesComponent.prototype.onValueChanged = function (data) {
        if (!this.generateAddressesForm) {
            return;
        }
        var form = this.generateAddressesForm;
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
    GenerateAddressesComponent.prototype.onGenerateClicked = function () {
        var walletInfo = new wallet_info_1.WalletInfo(this.globalService.getWalletName());
        console.log("onGenerateClicked is not implemented");
        //this.apiService.getUnusedReceiveAddresses(walletInfo, this.generateAddressesForm.get("generateAddresses").value)
        //  .subscribe(
        //    response => {
        //      this.addresses = response;
        //    }
        //  );
    };
    GenerateAddressesComponent.prototype.onBackClicked = function () {
        this.addresses = [''];
    };
    GenerateAddressesComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-generate-addresses',
            templateUrl: './generate-addresses.component.html',
            styleUrls: ['./generate-addresses.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, modal_service_1.ModalService, forms_1.FormBuilder])
    ], GenerateAddressesComponent);
    return GenerateAddressesComponent;
}());
exports.GenerateAddressesComponent = GenerateAddressesComponent;
//# sourceMappingURL=generate-addresses.component.js.map