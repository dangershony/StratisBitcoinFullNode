"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var setup_component_1 = require("./setup.component");
var create_component_1 = require("./create/create.component");
var show_mnemonic_component_1 = require("./create/show-mnemonic/show-mnemonic.component");
var confirm_mnemonic_component_1 = require("./create/confirm-mnemonic/confirm-mnemonic.component");
var recover_component_1 = require("./recover/recover.component");
var routes = [
    { path: 'setup', component: setup_component_1.SetupComponent },
    { path: 'setup/create', component: create_component_1.CreateComponent },
    { path: 'setup/create/show-mnemonic', component: show_mnemonic_component_1.ShowMnemonicComponent },
    { path: 'setup/create/confirm-mnemonic', component: confirm_mnemonic_component_1.ConfirmMnemonicComponent },
    { path: 'setup/recover', component: recover_component_1.RecoverComponent }
];
var SetupRoutingModule = /** @class */ (function () {
    function SetupRoutingModule() {
    }
    SetupRoutingModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [router_1.RouterModule.forChild(routes)]
        })
    ], SetupRoutingModule);
    return SetupRoutingModule;
}());
exports.SetupRoutingModule = SetupRoutingModule;
//# sourceMappingURL=setup-routing.module.js.map