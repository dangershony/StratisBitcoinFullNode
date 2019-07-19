"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var platform_browser_1 = require("@angular/platform-browser");
var http_1 = require("@angular/common/http");
var shared_module_1 = require("@shared/shared.module");
var app_routing_module_1 = require("./app-routing.module");
var app_component_1 = require("./app.component");
var api_interceptor_1 = require("@shared/http-interceptors/api-interceptor");
var login_component_1 = require("./login/login.component");
var setup_module_1 = require("./setup/setup.module");
var wallet_module_1 = require("./wallet/wallet.module");
var AppModule = /** @class */ (function () {
    function AppModule() {
    }
    AppModule = tslib_1.__decorate([
        core_1.NgModule({
            imports: [
                platform_browser_1.BrowserModule,
                http_1.HttpClientModule,
                shared_module_1.SharedModule,
                setup_module_1.SetupModule,
                wallet_module_1.WalletModule,
                app_routing_module_1.AppRoutingModule
            ],
            declarations: [
                app_component_1.AppComponent,
                login_component_1.LoginComponent
            ],
            providers: [{ provide: http_1.HTTP_INTERCEPTORS, useClass: api_interceptor_1.ApiInterceptor, multi: true }],
            bootstrap: [app_component_1.AppComponent]
        })
    ], AppModule);
    return AppModule;
}());
exports.AppModule = AppModule;
//# sourceMappingURL=app.module.js.map