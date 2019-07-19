"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var wallet_component_1 = require("./wallet.component");
var history_component_1 = require("./history/history.component");
var dashboard_component_1 = require("./dashboard/dashboard.component");
var overview_component_1 = require("./cold-staking/components/overview/overview.component");
var advanced_component_1 = require("./advanced/advanced.component");
var address_book_component_1 = require("./address-book/address-book.component");
var ext_pubkey_component_1 = require("./advanced/components/ext-pubkey/ext-pubkey.component");
var about_component_1 = require("./advanced/components/about/about.component");
var generate_addresses_component_1 = require("./advanced/components/generate-addresses/generate-addresses.component");
var resync_component_1 = require("./advanced/components/resync/resync.component");
var routes = [
    { path: 'wallet', component: wallet_component_1.WalletComponent, children: [
            { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
            { path: 'dashboard', component: dashboard_component_1.DashboardComponent },
            { path: 'history', component: history_component_1.HistoryComponent },
            { path: 'staking', component: overview_component_1.ColdStakingOverviewComponent },
            { path: 'advanced', component: advanced_component_1.AdvancedComponent,
                children: [
                    { path: '', redirectTo: 'about', pathMatch: 'full' },
                    { path: 'about', component: about_component_1.AboutComponent },
                    { path: 'extpubkey', component: ext_pubkey_component_1.ExtPubkeyComponent },
                    { path: 'generate-addresses', component: generate_addresses_component_1.GenerateAddressesComponent },
                    { path: 'resync', component: resync_component_1.ResyncComponent }
                ]
            },
            { path: 'address-book', component: address_book_component_1.AddressBookComponent }
        ] },
];
var WalletRoutingModule = /** @class */ (function () {
    function WalletRoutingModule() {
    }
    WalletRoutingModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [router_1.RouterModule.forChild(routes)],
            exports: [router_1.RouterModule]
        })
    ], WalletRoutingModule);
    return WalletRoutingModule;
}());
exports.WalletRoutingModule = WalletRoutingModule;
//# sourceMappingURL=wallet-routing.module.js.map