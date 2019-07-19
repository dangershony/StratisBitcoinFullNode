"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var api_service_1 = require("@shared/services/api.service");
var global_service_1 = require("@shared/services/global.service");
var modal_service_1 = require("@shared/services/modal.service");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var forms_1 = require("@angular/forms");
var ExtPubkeyComponent = /** @class */ (function () {
    function ExtPubkeyComponent(apiService, globalService, genericModalService, fb) {
        this.apiService = apiService;
        this.globalService = globalService;
        this.genericModalService = genericModalService;
        this.fb = fb;
        this.passphraseValidator = forms_1.Validators.compose([forms_1.Validators.required]);
        this.formErrors = {
            'walletPassword': '',
            'dump': ''
        };
        this.validationMessages = {
            'generateAddresses': {
                'required': 'Please paste at least one key.',
                'min': 'Not enough data.',
                'max': 'Too many lines.'
            },
            'walletPassword': {
                'required': 'A passphrase is required',
                'pattern': 'A passphrase must be from 12 to 60 characters long and contain only lowercase and uppercase latin characters and numbers.'
            },
        };
    }
    ExtPubkeyComponent.prototype.ngOnInit = function () {
        this.buildForm();
    };
    ExtPubkeyComponent.prototype.buildForm = function () {
        var _this = this;
        this.dumpForm = this.fb.group({
            "walletPassword": ["", this.passphraseValidator],
            "dump": []
        });
        this.dumpForm.valueChanges
            .subscribe(function (data) { return _this.onValueChanged(data); });
        this.onValueChanged();
    };
    ExtPubkeyComponent.prototype.onValueChanged = function (data) {
        if (!this.dumpForm) {
            return;
        }
        var form = this.dumpForm;
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
    ExtPubkeyComponent.prototype.onImportClicked = function () {
        this.importKeys();
    };
    ExtPubkeyComponent.prototype.onExportClicked = function () {
        this.exportKeys();
    };
    ExtPubkeyComponent.prototype.importKeys = function () {
        var data = this.dumpForm.get("dump").value;
        var pw = this.dumpForm.get("walletPassword").value;
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("importKeys", { keys: data, walletPassphrase: pw }), this.onImportKeys.bind(this));
    };
    ExtPubkeyComponent.prototype.onImportKeys = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.message = responsePayload.responsePayload.message;
            this.importedAddresses = responsePayload.responsePayload.importedAddresses;
            this.importedAddresses.forEach(function (item) {
                console.log(item);
            });
        }
        else {
            this.message = responsePayload.statusText;
        }
    };
    ExtPubkeyComponent.prototype.exportKeys = function () {
        var pw = this.dumpForm.get("walletPassword").value;
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("exportKeys", { walletPassphrase: pw }), this.onExportKeys.bind(this));
    };
    ExtPubkeyComponent.prototype.onExportKeys = function (responsePayload) {
        if (responsePayload.status === 200) {
            this.export = responsePayload.responsePayload.message;
        }
        else {
            this.message = responsePayload.statusText;
            this.export = responsePayload.responsePayload.message;
        }
    };
    ExtPubkeyComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-ext-pubkey',
            templateUrl: './ext-pubkey.component.html',
            styleUrls: ['./ext-pubkey.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [api_service_1.ApiService, global_service_1.GlobalService, modal_service_1.ModalService, forms_1.FormBuilder])
    ], ExtPubkeyComponent);
    return ExtPubkeyComponent;
}());
exports.ExtPubkeyComponent = ExtPubkeyComponent;
//# sourceMappingURL=ext-pubkey.component.js.map