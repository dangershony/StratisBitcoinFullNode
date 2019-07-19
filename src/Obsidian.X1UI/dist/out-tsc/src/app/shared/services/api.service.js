"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var router_1 = require("@angular/router");
var http_1 = require("@angular/common/http");
var rxjs_1 = require("rxjs");
var operators_1 = require("rxjs/operators");
var global_service_1 = require("./global.service");
var modal_service_1 = require("./modal.service");
var visualcrypt_light_js_1 = require("@shared/services/visualcrypt-light.js");
var visualcrypt_dtos_1 = require("@shared/services/visualcrypt-dtos");
var ApiService = /** @class */ (function () {
    function ApiService(http, globalService, modalService, router) {
        this.http = http;
        this.globalService = globalService;
        this.modalService = modalService;
        this.router = router;
    }
    ApiService_1 = ApiService;
    ApiService.prototype.makeRequest = function (arg, callback) {
        var request = null;
        console.log("Begin request: " + arg.command);
        if (arg.command === "getKey") {
            var clientKeyPair = visualcrypt_light_js_1.vcl.generateKeyPair();
            ApiService_1.clientPrivateKey = clientKeyPair.private;
            ApiService_1.clientPublicKey = clientKeyPair.public;
            request = visualcrypt_light_js_1.vcl.createModel(ApiService_1.clientPublicKey);
        }
        else {
            if (!ApiService_1.serverPublicKey) {
                throw "No server public key!";
            }
            else {
                arg.target = this.globalService.getWalletName();
                var json = JSON.stringify(arg);
                var jsonBytes = new TextEncoder().encode(json);
                var cipherV2Bytes = visualcrypt_light_js_1.vcl.encrypt(jsonBytes, ApiService_1.serverPublicKey, ApiService_1.serverAuthKey, ApiService_1.clientPrivateKey);
                request = visualcrypt_light_js_1.vcl.createModel(ApiService_1.clientPublicKey, cipherV2Bytes);
            }
        }
        console.log("URL: " + this.getApiUrl());
        this.http.post(this.getApiUrl(), request)
            .subscribe(function (response) {
            ApiService_1.serverPublicKey = visualcrypt_light_js_1.vcl.hexStringToBytes(response.currentPublicKey);
            ApiService_1.serverAuthKey = visualcrypt_light_js_1.vcl.hexStringToBytes(response.authKey);
            ApiService_1.serverAuthKeyHex = response.authKey;
            if (response.cipherV2Bytes) {
                var decrypted = visualcrypt_light_js_1.vcl.decrypt(visualcrypt_light_js_1.vcl.hexStringToBytes(response.cipherV2Bytes), ApiService_1.serverPublicKey, ApiService_1.serverAuthKey, ApiService_1.clientPrivateKey);
                var json = new TextDecoder().decode(decrypted);
                var responsePayload = JSON.parse(json);
                if (responsePayload.status !== 200) {
                    console.log(arg.command + ":" + responsePayload.status + " - " + responsePayload.statusText);
                }
                callback(responsePayload);
            }
            else {
                var publicKeyPayload = new visualcrypt_dtos_1.ResponsePayload();
                publicKeyPayload.responsePayload = response;
                publicKeyPayload.status = 200;
                publicKeyPayload.statusText = "Ok";
                callback(publicKeyPayload);
            }
        }, function (error) {
            var errorPayload = new visualcrypt_dtos_1.ResponsePayload();
            errorPayload.status = error.status;
            errorPayload.statusText = error.status === 0 ? error.message : error.statusText;
            errorPayload.responsePayload = { errorDescription: error.message };
            callback(errorPayload);
        });
    };
    ApiService.prototype.getApiUrl = function () {
        return "http://" + this.globalService.getDaemonIP() + ":" + this.globalService.getApiPort() + "/SecureApi/ExecuteAsync";
    };
    ApiService.prototype.getAddressBookAddresses = function () {
        console.log("getAddressBookAddresses is not impl.");
        return null;
        //return this.pollingInterval.pipe(
        //  startWith(0),
        //  switchMap(() => this.http.get(this.getApiUrl() + '/AddressBook')),
        //  catchError(err => this.handleHttpError(err))
        //);
    };
    ApiService.prototype.addAddressBookAddress = function (data) {
        var _this = this;
        return this.http.post(this.getApiUrl() + '/AddressBook/address', JSON.stringify(data)).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    ApiService.prototype.removeAddressBookAddress = function (label) {
        var _this = this;
        var params = new http_1.HttpParams().set('label', label);
        return this.http.delete(this.getApiUrl() + '/AddressBook/address', { params: params }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /** Gets the extended public key from a certain wallet */
    ApiService.prototype.getExtPubkey = function (data) {
        var _this = this;
        var params = new http_1.HttpParams()
            .set('walletName', data.walletName)
            .set('accountName', 'account 0');
        return this.http.get(this.getApiUrl() + '/segwitwallet/extpubkey', { params: params }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
    * Get a new mnemonic
    */
    ApiService.prototype.getNewMnemonic = function () {
        var _this = this;
        var params = new http_1.HttpParams()
            .set('language', 'English')
            .set('wordCount', '12');
        return this.http.get(this.getApiUrl() + '/segwitwallet/mnemonic', { params: params }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Create a new Stratis wallet.
     */
    ApiService.prototype.createStratisWallet = function (data) {
        var _this = this;
        return this.http.post(this.getApiUrl() + '/segwitwallet/create/', JSON.stringify(data)).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Recover a Stratis wallet.
     */
    ApiService.prototype.recoverStratisWallet = function (data) {
        var _this = this;
        return this.http.post(this.getApiUrl() + '/segwitwallet/recover/', JSON.stringify(data)).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Get wallet status info from the API.
     */
    ApiService.prototype.getWalletStatus = function () {
        var _this = this;
        console.log("getWalletStatus()");
        return this.http.get(this.getApiUrl() + '/segwitwallet/status').pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Get the maximum sendable amount for a given fee from the API
     */
    ApiService.prototype.getMaximumBalance = function (data) {
        var _this = this;
        console.log("getMaximumBalance()");
        var params = new http_1.HttpParams()
            .set('walletName', data.walletName)
            .set('accountName', "account 0")
            .set('feeType', data.feeType)
            .set('allowUnconfirmed', "true");
        return this.http.get(this.getApiUrl() + '/segwitwallet/maxbalance', { params: params }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
    * Estimate the fee of a transaction
    */
    ApiService.prototype.estimateFee = function (data) {
        var _this = this;
        console.log("estimateFee()");
        return this.http.post(this.getApiUrl() + '/segwitwallet/estimate-txfee', {
            'walletName': data.walletName,
            'accountName': data.accountName,
            'recipients': [
                {
                    'destinationAddress': data.recipients[0].destinationAddress,
                    'amount': data.recipients[0].amount
                }
            ],
            'feeType': data.feeType,
            'allowUnconfirmed': true
        }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /** Remove transaction */
    ApiService.prototype.removeTransaction = function (walletName) {
        var _this = this;
        console.log("removeTransaction()");
        var params = new http_1.HttpParams()
            .set('walletName', walletName)
            .set('all', 'true')
            .set('resync', 'true');
        return this.http.delete(this.getApiUrl() + '/segwitwallet/remove-transactions', { params: params }).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Start staking
     */
    ApiService.prototype.startStaking = function (data) {
        var _this = this;
        console.log("startStaking()");
        return this.http.post(this.getApiUrl() + '/staking/startstaking', JSON.stringify(data)).pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
      * Stop staking
      */
    ApiService.prototype.stopStaking = function () {
        var _this = this;
        console.log("stopStaking()");
        return this.http.post(this.getApiUrl() + '/staking/stopstaking', 'true').pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    /**
     * Send shutdown signal to the daemon
     */
    ApiService.prototype.shutdownNode = function () {
        var _this = this;
        console.log("shutdownNode()");
        return this.http.post(this.getApiUrl() + '/node/shutdown', 'corsProtection:true').pipe(operators_1.catchError(function (err) { return _this.handleHttpError(err); }));
    };
    ApiService.prototype.handleHttpError = function (error, silent) {
        console.log(error);
        if (error.status === 0) {
            if (!silent) {
                this.modalService.openModal(null, null);
                this.router.navigate(['app']);
            }
        }
        else if (error.status >= 400) {
            if (!error.error.errors[0].message) {
                console.log(error);
            }
            else {
                this.modalService.openModal(null, error.error.errors[0].message);
            }
        }
        return rxjs_1.throwError(error);
    };
    var ApiService_1;
    ApiService = ApiService_1 = tslib_1.__decorate([
        core_1.Injectable({
            providedIn: 'root'
        }),
        tslib_1.__metadata("design:paramtypes", [http_1.HttpClient, global_service_1.GlobalService, modal_service_1.ModalService, router_1.Router])
    ], ApiService);
    return ApiService;
}());
exports.ApiService = ApiService;
//# sourceMappingURL=api.service.js.map