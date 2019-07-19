"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var setup_component_1 = require("./setup.component");
var create_component_1 = require("./create/create.component");
var shared_module_1 = require("@shared/shared.module");
var setup_routing_module_1 = require("./setup-routing.module");
var recover_component_1 = require("./recover/recover.component");
var show_mnemonic_component_1 = require("./create/show-mnemonic/show-mnemonic.component");
var confirm_mnemonic_component_1 = require("./create/confirm-mnemonic/confirm-mnemonic.component");
var ngx_bootstrap_1 = require("ngx-bootstrap");
var SetupModule = /** @class */ (function () {
    function SetupModule() {
    }
    SetupModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [
                setup_routing_module_1.SetupRoutingModule,
                shared_module_1.SharedModule,
                ngx_bootstrap_1.BsDatepickerModule.forRoot()
            ],
            declarations: [
                create_component_1.CreateComponent,
                setup_component_1.SetupComponent,
                recover_component_1.RecoverComponent,
                show_mnemonic_component_1.ShowMnemonicComponent,
                confirm_mnemonic_component_1.ConfirmMnemonicComponent
            ]
        })
    ], SetupModule);
    return SetupModule;
}());
exports.SetupModule = SetupModule;
//# sourceMappingURL=setup.module.js.map