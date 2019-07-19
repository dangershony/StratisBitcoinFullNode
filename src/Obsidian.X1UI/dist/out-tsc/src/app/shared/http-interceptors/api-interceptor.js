"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var ApiInterceptor = /** @class */ (function () {
    function ApiInterceptor() {
    }
    ApiInterceptor.prototype.intercept = function (req, next) {
        var finalReq = req.clone({
            headers: req.headers.set('Content-Type', 'application/json')
        });
        return next.handle(finalReq);
    };
    ApiInterceptor = tslib_1.__decorate([
        core_1.Injectable(),
        tslib_1.__metadata("design:paramtypes", [])
    ], ApiInterceptor);
    return ApiInterceptor;
}());
exports.ApiInterceptor = ApiInterceptor;
//# sourceMappingURL=api-interceptor.js.map