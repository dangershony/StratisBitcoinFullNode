"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var RequestObject = /** @class */ (function () {
    function RequestObject(name, payload) {
        this.command = name;
        this.payload = JSON.stringify(payload);
    }
    return RequestObject;
}());
exports.RequestObject = RequestObject;
var ResponseWrapper = /** @class */ (function () {
    function ResponseWrapper() {
    }
    return ResponseWrapper;
}());
exports.ResponseWrapper = ResponseWrapper;
;
var ResponsePayload = /** @class */ (function () {
    function ResponsePayload() {
    }
    return ResponsePayload;
}());
exports.ResponsePayload = ResponsePayload;
//# sourceMappingURL=visualcrypt-dtos.js.map