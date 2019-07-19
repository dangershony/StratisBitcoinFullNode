"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var common_1 = require("@angular/common");
var coin_notation_pipe_1 = require("./pipes/coin-notation.pipe");
var number_to_string_pipe_1 = require("./pipes/number-to-string.pipe");
var auto_focus_directive_1 = require("./directives/auto-focus.directive");
var password_validation_directive_1 = require("./directives/password-validation.directive");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var ngx_electron_1 = require("ngx-electron");
var ngx_qrcode2_1 = require("ngx-qrcode2");
var ngx_pagination_1 = require("ngx-pagination");
var ngx_clipboard_1 = require("ngx-clipboard");
var forms_1 = require("@angular/forms");
var generic_modal_component_1 = require("./components/generic-modal/generic-modal.component");
var SharedModule = /** @class */ (function () {
    function SharedModule() {
    }
    SharedModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [common_1.CommonModule],
            declarations: [coin_notation_pipe_1.CoinNotationPipe, number_to_string_pipe_1.NumberToStringPipe, auto_focus_directive_1.AutoFocusDirective, password_validation_directive_1.PasswordValidationDirective, generic_modal_component_1.GenericModalComponent],
            exports: [common_1.CommonModule, forms_1.ReactiveFormsModule, forms_1.FormsModule, ng_bootstrap_1.NgbModule, ngx_electron_1.NgxElectronModule, ngx_qrcode2_1.NgxQRCodeModule, ngx_pagination_1.NgxPaginationModule, ngx_clipboard_1.ClipboardModule, generic_modal_component_1.GenericModalComponent, coin_notation_pipe_1.CoinNotationPipe, number_to_string_pipe_1.NumberToStringPipe, auto_focus_directive_1.AutoFocusDirective, password_validation_directive_1.PasswordValidationDirective],
            entryComponents: [generic_modal_component_1.GenericModalComponent]
        })
    ], SharedModule);
    return SharedModule;
}());
exports.SharedModule = SharedModule;
//# sourceMappingURL=shared.module.js.map