"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var tslib_1 = require("tslib");
var core_1 = require("@angular/core");
var AutoFocusDirective = /** @class */ (function () {
    function AutoFocusDirective(renderer, elementRef) {
        this.renderer = renderer;
        this.elementRef = elementRef;
    }
    AutoFocusDirective.prototype.ngOnInit = function () {
        this.renderer.invokeElementMethod(this.elementRef.nativeElement, 'focus', []);
    };
    AutoFocusDirective = tslib_1.__decorate([
        core_1.Directive({
            selector: '[myAutoFocus]'
        }),
        tslib_1.__metadata("design:paramtypes", [core_1.Renderer, core_1.ElementRef])
    ], AutoFocusDirective);
    return AutoFocusDirective;
}());
exports.AutoFocusDirective = AutoFocusDirective;
//# sourceMappingURL=auto-focus.directive.js.map