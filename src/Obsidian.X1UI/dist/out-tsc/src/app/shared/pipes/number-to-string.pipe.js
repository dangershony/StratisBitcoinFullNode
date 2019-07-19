"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var NumberToStringPipe = /** @class */ (function () {
    function NumberToStringPipe() {
    }
    NumberToStringPipe.prototype.transform = function (num) {
        if (isNaN(num)) {
            return '0';
        }
        var numStr = String(num);
        if (Math.abs(num) < 1.0) {
            var e = parseInt(num.toString().split('e-')[1]);
            if (e) {
                var negative = num < 0;
                if (negative) {
                    num *= -1;
                }
                num *= Math.pow(10, e - 1);
                numStr = '0.' + (new Array(e)).join('0') + num.toString().substring(2);
                if (negative) {
                    numStr = '-' + numStr;
                    return numStr;
                }
            }
        }
        else {
            var e = parseInt(num.toString().split('+')[1]);
            if (e > 20) {
                e -= 20;
                num /= Math.pow(10, e);
                numStr = num.toString() + (new Array(e + 1)).join('0');
            }
        }
        return numStr;
    };
    NumberToStringPipe = tslib_1.__decorate([
        core_1.Pipe({
            name: 'numberToString'
        }),
        tslib_1.__metadata("design:paramtypes", [])
    ], NumberToStringPipe);
    return NumberToStringPipe;
}());
exports.NumberToStringPipe = NumberToStringPipe;
//# sourceMappingURL=number-to-string.pipe.js.map