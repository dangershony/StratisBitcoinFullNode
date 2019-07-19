"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var common_1 = require("@angular/common");
var ng_bootstrap_1 = require("@ng-bootstrap/ng-bootstrap");
var ngx_clipboard_1 = require("ngx-clipboard");
var forms_1 = require("@angular/forms");
var cold_staking_service_1 = require("./cold-staking.service");
var overview_component_1 = require("./components/overview/overview.component");
var history_component_1 = require("./components/overview/history/history.component");
var wallet_component_1 = require("./components/overview/wallet/wallet.component");
var create_address_component_1 = require("./components/modals/create-address/create-address.component");
var withdraw_component_1 = require("./components/modals/withdraw/withdraw.component");
var create_component_1 = require("./components/modals/create/create.component");
var create_success_component_1 = require("./components/modals/create-success/create-success.component");
var ColdStakingModule = /** @class */ (function () {
    function ColdStakingModule() {
    }
    ColdStakingModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [
                common_1.CommonModule, ng_bootstrap_1.NgbModalModule, ngx_clipboard_1.ClipboardModule, forms_1.FormsModule, forms_1.ReactiveFormsModule
            ],
            providers: [{ provide: cold_staking_service_1.ColdStakingServiceBase, useClass: cold_staking_service_1.FakeColdStakingService }],
            declarations: [overview_component_1.ColdStakingOverviewComponent,
                history_component_1.ColdStakingHistoryComponent,
                wallet_component_1.ColdStakingWalletComponent,
                create_address_component_1.ColdStakingCreateAddressComponent,
                withdraw_component_1.ColdStakingWithdrawComponent,
                create_component_1.ColdStakingCreateComponent,
                create_success_component_1.ColdStakingCreateSuccessComponent],
            entryComponents: [create_address_component_1.ColdStakingCreateAddressComponent,
                withdraw_component_1.ColdStakingWithdrawComponent,
                create_component_1.ColdStakingCreateComponent,
                create_success_component_1.ColdStakingCreateSuccessComponent]
        })
    ], ColdStakingModule);
    return ColdStakingModule;
}());
exports.ColdStakingModule = ColdStakingModule;
//# sourceMappingURL=cold-staking.module.js.map