"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var rxjs_1 = require("rxjs");
var ColdStakingInfo = /** @class */ (function () {
    function ColdStakingInfo(hotWalletBalance, coldWalletBalance, hotWalletAmount, coldWalletAmount) {
        this.hotWalletBalance = hotWalletBalance;
        this.coldWalletBalance = coldWalletBalance;
        this.hotWalletAmount = hotWalletAmount;
        this.coldWalletAmount = coldWalletAmount;
    }
    return ColdStakingInfo;
}());
exports.ColdStakingInfo = ColdStakingInfo;
var ColdStakingHistoryItem = /** @class */ (function () {
    function ColdStakingHistoryItem(status, side, amount, dateTime, wallet) {
        this.status = status;
        this.side = side;
        this.amount = amount;
        this.dateTime = dateTime;
        this.wallet = wallet;
    }
    return ColdStakingHistoryItem;
}());
exports.ColdStakingHistoryItem = ColdStakingHistoryItem;
var ColdStakingServiceBase = /** @class */ (function () {
    function ColdStakingServiceBase() {
    }
    ColdStakingServiceBase.prototype.GetInfo = function (walletName) { return rxjs_1.of(); };
    ColdStakingServiceBase.prototype.GetHistory = function (walletName) { return rxjs_1.of(); };
    ColdStakingServiceBase.prototype.GetAddress = function (walletName) { return rxjs_1.of(); };
    ColdStakingServiceBase.prototype.CreateColdstaking = function () {
        var params = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            params[_i] = arguments[_i];
        }
        return rxjs_1.of();
    };
    return ColdStakingServiceBase;
}());
exports.ColdStakingServiceBase = ColdStakingServiceBase;
var FakeColdStakingService = /** @class */ (function () {
    function FakeColdStakingService() {
    }
    FakeColdStakingService.prototype.GetInfo = function (walletName) {
        return rxjs_1.of(new ColdStakingInfo(88025, 91223, 4000, 28765));
    };
    FakeColdStakingService.prototype.GetHistory = function (walletName) {
        return rxjs_1.of([
            new ColdStakingHistoryItem('warning', 'hot', '+1.0000000', '26/11/2017 15:31', 'Breeze2'),
            new ColdStakingHistoryItem('success', 'hot', '+1.0000000', '26/11/2017 15:31', 'Breeze2'),
            new ColdStakingHistoryItem('success', 'cold', '-1.0037993', '26/11/2017 15:31', 'Breeze2')
        ]);
    };
    FakeColdStakingService.prototype.GetAddress = function (walletName) {
        return rxjs_1.of('ScCHt2Mug856o1E6gck6VFriXYnRYBD8NE');
    };
    FakeColdStakingService.prototype.CreateColdstaking = function () {
        var params = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            params[_i] = arguments[_i];
        }
        return rxjs_1.of(true);
    };
    FakeColdStakingService = tslib_1.__decorate([
        core_1.Injectable()
    ], FakeColdStakingService);
    return FakeColdStakingService;
}());
exports.FakeColdStakingService = FakeColdStakingService;
//# sourceMappingURL=cold-staking.service.js.map