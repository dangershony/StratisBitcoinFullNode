"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var PasswordValidationDirective = /** @class */ (function () {
    function PasswordValidationDirective() {
    }
    PasswordValidationDirective.MatchPassword = function (AC) {
        var password = AC.get('walletPassword').value;
        var confirmPassword = AC.get('walletPasswordConfirmation').value;
        if (confirmPassword !== password) {
            AC.get('walletPasswordConfirmation').setErrors({ walletPasswordConfirmation: true });
        }
        else {
            AC.get('walletPasswordConfirmation').setErrors(null);
            return null;
        }
    };
    PasswordValidationDirective = tslib_1.__decorate([
        core_1.Directive({
            selector: '[appPasswordValidation]'
        }),
        tslib_1.__metadata("design:paramtypes", [])
    ], PasswordValidationDirective);
    return PasswordValidationDirective;
}());
exports.PasswordValidationDirective = PasswordValidationDirective;
//# sourceMappingURL=password-validation.directive.js.map