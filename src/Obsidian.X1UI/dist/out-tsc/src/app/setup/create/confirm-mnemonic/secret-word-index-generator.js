"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var SecretWordIndexGenerator = /** @class */ (function () {
    function SecretWordIndexGenerator() {
        this.textPrefix = 'Word number ';
        var getRandom = function () {
            var taken = [];
            for (var _i = 0; _i < arguments.length; _i++) {
                taken[_i] = arguments[_i];
            }
            var min = 0, max = 11;
            var getRandom = function () { return Math.floor(Math.random() * (max - min + 1) + min); };
            var random = 0;
            while (taken.includes(random = getRandom())) { }
            return random;
        };
        var one = getRandom();
        var two = getRandom(one);
        var three = getRandom(one, two);
        var indexes = [one, two, three].sort(function (a, b) { return a - b; });
        this.index1 = indexes[0];
        this.index2 = indexes[1];
        this.index3 = indexes[2];
        this.text1 = "" + this.textPrefix + (this.index1 + 1);
        this.text2 = "" + this.textPrefix + (this.index2 + 1);
        this.text3 = "" + this.textPrefix + (this.index3 + 1);
    }
    return SecretWordIndexGenerator;
}());
exports.SecretWordIndexGenerator = SecretWordIndexGenerator;
//# sourceMappingURL=secret-word-index-generator.js.map