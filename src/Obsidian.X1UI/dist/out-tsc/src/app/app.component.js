"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var platform_browser_1 = require("@angular/platform-browser");
var api_service_1 = require("@shared/services/api.service");
var ngx_electron_1 = require("ngx-electron");
var global_service_1 = require("@shared/services/global.service");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var AppComponent = /** @class */ (function () {
    function AppComponent(router, apiService, globalService, titleService, electronService) {
        this.router = router;
        this.apiService = apiService;
        this.globalService = globalService;
        this.titleService = titleService;
        this.electronService = electronService;
        this.maxRetryCount = 60 * 60 * 3;
        this.tryDelayMilliseconds = 1000;
        this.retries = 0;
        this.loading = true;
        this.loadingFailed = false;
        this.isDestroyed = false;
    }
    AppComponent.prototype.ngOnInit = function () {
        this.loadServerPrivateKey();
        this.polling = setInterval(this.loadServerPrivateKey.bind(this), this.tryDelayMilliseconds);
        this.setTitle();
    };
    AppComponent.prototype.ngOnDestroy = function () {
        if (this.isDestroyed)
            return;
        clearInterval(this.polling);
        this.loading = false;
        this.isDestroyed = true;
    };
    AppComponent.prototype.loadServerPrivateKey = function () {
        this.apiService.makeRequest(new visualcrypt_dtos_1.RequestObject("getKey", ""), this.onLoadServerPrivateKey.bind(this));
    };
    AppComponent.prototype.onLoadServerPrivateKey = function (responsePayload) {
        if ((responsePayload.status !== 200 || !api_service_1.ApiService.serverPublicKey) && this.retries < this.maxRetryCount) {
            this.retries++;
        }
        else {
            if (!api_service_1.ApiService.serverPublicKey) {
                console.log("Failed to get the server's public key after " + this.retries + " retries, giving up.");
                this.loading = false;
                this.loadingFailed = true;
            }
            else {
                console.log("Received the server's public key.");
                this.ngOnDestroy();
                this.router.navigate(["/login"]);
            }
        }
    };
    AppComponent.prototype.setTitle = function () {
        var applicationName = "ObsidianX";
        var testnetSuffix = this.globalService.getTestnetEnabled() ? ' (testnet)' : '';
        var title = applicationName + " " + this.globalService.getApplicationVersion() + testnetSuffix;
        this.titleService.setTitle(title);
    };
    AppComponent.prototype.openSupport = function () {
        this.electronService.shell.openExternal('https://github.com/stratisproject/StratisCore/');
    };
    AppComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-root',
            templateUrl: './app.component.html',
            styleUrls: ['./app.component.css'],
        }),
        tslib_1.__metadata("design:paramtypes", [router_1.Router, api_service_1.ApiService, global_service_1.GlobalService, platform_browser_1.Title, ngx_electron_1.ElectronService])
    ], AppComponent);
    return AppComponent;
}());
exports.AppComponent = AppComponent;
//# sourceMappingURL=app.component.js.map