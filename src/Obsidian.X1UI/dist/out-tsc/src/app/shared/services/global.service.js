"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ngx_electron_1 = require("ngx-electron");
var GlobalService = /** @class */ (function () {
    function GlobalService(electronService) {
        this.electronService = electronService;
        this.applicationVersion = "1.1.1";
        this.testnet = false;
        this.sidechain = false;
        this.setApplicationVersion();
        this.setTestnetEnabled();
        this.setDaemonIP(null);
    }
    GlobalService.prototype.getApplicationVersion = function () {
        return this.applicationVersion;
    };
    GlobalService.prototype.setApplicationVersion = function () {
        if (this.electronService.isElectronApp) {
            this.applicationVersion = this.electronService.remote.app.getVersion();
        }
    };
    GlobalService.prototype.getTestnetEnabled = function () {
        return this.testnet;
    };
    GlobalService.prototype.setTestnetEnabled = function () {
        if (this.electronService.isElectronApp) {
            this.testnet = this.electronService.ipcRenderer.sendSync('get-testnet');
        }
    };
    GlobalService.prototype.getSidechainEnabled = function () {
        return this.sidechain;
    };
    GlobalService.prototype.getApiPort = function () {
        return 37777;
    };
    GlobalService.prototype.getWalletPath = function () {
        return this.walletPath;
    };
    GlobalService.prototype.setWalletPath = function (walletPath) {
        this.walletPath = walletPath;
    };
    GlobalService.prototype.getNetwork = function () {
        return this.network;
    };
    GlobalService.prototype.setNetwork = function (network) {
        this.network = network;
    };
    GlobalService.prototype.getWalletName = function () {
        return this.currentWalletName;
    };
    GlobalService.prototype.setWalletName = function (currentWalletName) {
        this.currentWalletName = currentWalletName;
    };
    GlobalService.prototype.getCoinUnit = function () {
        return this.coinUnit;
    };
    GlobalService.prototype.setCoinUnit = function (coinUnit) {
        this.coinUnit = coinUnit;
    };
    GlobalService.prototype.getDaemonIP = function () {
        return this.daemonIP;
    };
    GlobalService.prototype.setDaemonIP = function (ipAddress) {
        if (ipAddress) {
            this.daemonIP = ipAddress;
        }
        else {
            if (this.electronService.isElectronApp) {
                this.daemonIP = this.electronService.ipcRenderer.sendSync('get-daemonip');
            }
            else {
                this.daemonIP = 'localhost';
            }
        }
    };
    GlobalService = tslib_1.__decorate([
        core_1.Injectable({
            providedIn: 'root'
        }),
        tslib_1.__metadata("design:paramtypes", [ngx_electron_1.ElectronService])
    ], GlobalService);
    return GlobalService;
}());
exports.GlobalService = GlobalService;
//# sourceMappingURL=global.service.js.map