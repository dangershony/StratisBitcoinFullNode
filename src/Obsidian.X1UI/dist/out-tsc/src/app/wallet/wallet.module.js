"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var shared_module_1 = require("@shared/shared.module");
var wallet_routing_module_1 = require("./wallet-routing.module");
var cold_staking_module_1 = require("./cold-staking/cold-staking.module");
var wallet_component_1 = require("./wallet.component");
var menu_component_1 = require("./menu/menu.component");
var dashboard_component_1 = require("./dashboard/dashboard.component");
var history_component_1 = require("./history/history.component");
var status_bar_component_1 = require("./status-bar/status-bar.component");
var advanced_component_1 = require("./advanced/advanced.component");
var address_book_component_1 = require("./address-book/address-book.component");
var add_new_address_component_1 = require("./address-book/modals/add-new-address/add-new-address.component");
var ext_pubkey_component_1 = require("./advanced/components/ext-pubkey/ext-pubkey.component");
var about_component_1 = require("./advanced/components/about/about.component");
var generate_addresses_component_1 = require("./advanced/components/generate-addresses/generate-addresses.component");
var resync_component_1 = require("./advanced/components/resync/resync.component");
var send_component_1 = require("./send/send.component");
var receive_component_1 = require("./receive/receive.component");
var send_confirmation_component_1 = require("./send/send-confirmation/send-confirmation.component");
var transaction_details_component_1 = require("./transaction-details/transaction-details.component");
var logout_confirmation_component_1 = require("./logout-confirmation/logout-confirmation.component");
var ngx_bootstrap_1 = require("ngx-bootstrap");
var WalletModule = /** @class */ (function () {
    function WalletModule() {
    }
    WalletModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [
                shared_module_1.SharedModule,
                wallet_routing_module_1.WalletRoutingModule,
                cold_staking_module_1.ColdStakingModule,
                ngx_bootstrap_1.BsDatepickerModule.forRoot()
            ],
            declarations: [
                wallet_component_1.WalletComponent,
                menu_component_1.MenuComponent,
                dashboard_component_1.DashboardComponent,
                send_component_1.SendComponent,
                receive_component_1.ReceiveComponent,
                send_confirmation_component_1.SendConfirmationComponent,
                transaction_details_component_1.TransactionDetailsComponent,
                logout_confirmation_component_1.LogoutConfirmationComponent,
                history_component_1.HistoryComponent,
                status_bar_component_1.StatusBarComponent,
                advanced_component_1.AdvancedComponent,
                address_book_component_1.AddressBookComponent,
                add_new_address_component_1.AddNewAddressComponent,
                ext_pubkey_component_1.ExtPubkeyComponent,
                about_component_1.AboutComponent,
                generate_addresses_component_1.GenerateAddressesComponent,
                resync_component_1.ResyncComponent
            ],
            entryComponents: [
                send_component_1.SendComponent,
                send_confirmation_component_1.SendConfirmationComponent,
                receive_component_1.ReceiveComponent,
                transaction_details_component_1.TransactionDetailsComponent,
                logout_confirmation_component_1.LogoutConfirmationComponent
            ]
        })
    ], WalletModule);
    return WalletModule;
}());
exports.WalletModule = WalletModule;
//# sourceMappingURL=wallet.module.js.map