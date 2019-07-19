"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var CoinNotationPipe = /** @class */ (function () {
    function CoinNotationPipe() {
        this.decimalLimit = 8;
    }
    CoinNotationPipe.prototype.transform = function (value) {
        var temp;
        if (typeof value === 'number') {
            temp = value / 100000000;
            return temp.toFixed(this.decimalLimit);
        }
    };
    CoinNotationPipe = tslib_1.__decorate([
        core_1.Pipe({
            name: 'coinNotation'
        }),
        tslib_1.__metadata("design:paramtypes", [])
    ], CoinNotationPipe);
    return CoinNotationPipe;
}());
exports.CoinNotationPipe = CoinNotationPipe;
//# sourceMappingURL=coin-notation.pipe.js.map