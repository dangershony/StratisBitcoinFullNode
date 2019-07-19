"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var global_service_1 = require("@shared/services/global.service");
var cold_staking_service_1 = require("../../../cold-staking.service");
var HistoryItem = /** @class */ (function () {
    function HistoryItem(status, side, amount, dateTime, wallet) {
        this.status = status;
        this.side = side;
        this.amount = amount;
        this.dateTime = dateTime;
        this.wallet = wallet;
    }
    return HistoryItem;
}());
exports.HistoryItem = HistoryItem;
var ColdStakingHistoryComponent = /** @class */ (function () {
    function ColdStakingHistoryComponent(globalService, stakingService) {
        this.globalService = globalService;
        this.stakingService = stakingService;
        this.items = [];
    }
    ColdStakingHistoryComponent.prototype.ngOnInit = function () {
        var _this = this;
        this.stakingService.GetHistory(this.globalService.getWalletName()).subscribe(function (x) {
            _this.items = x.map(function (i) { return new HistoryItem(i.status, i.side, i.amount, i.dateTime, i.wallet); });
        });
    };
    ColdStakingHistoryComponent = tslib_1.__decorate([
        core_1.Component({
            selector: 'app-staking-history',
            templateUrl: './history.component.html',
            styleUrls: ['./history.component.css']
        }),
        tslib_1.__metadata("design:paramtypes", [global_service_1.GlobalService, cold_staking_service_1.ColdStakingServiceBase])
    ], ColdStakingHistoryComponent);
    return ColdStakingHistoryComponent;
}());
exports.ColdStakingHistoryComponent = ColdStakingHistoryComponent;
//# sourceMappingURL=history.component.js.map