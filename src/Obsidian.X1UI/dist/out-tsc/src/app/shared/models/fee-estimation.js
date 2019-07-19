"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var Recipient = /** @class */ (function () {
    function Recipient(destinationAddress, amount) {
        this.destinationAddress = destinationAddress;
        this.amount = amount;
    }
    return Recipient;
}());
exports.Recipient = Recipient;
var FeeEstimation = /** @class */ (function () {
    function FeeEstimation(walletName, accountName, destinationAddress, amount, feeType, allowUnconfirmed) {
        this.walletName = walletName;
        this.accountName = accountName;
        this.recipients = [new Recipient(destinationAddress, amount)];
        this.feeType = feeType;
        this.allowUnconfirmed = allowUnconfirmed;
    }
    return FeeEstimation;
}());
exports.FeeEstimation = FeeEstimation;
//# sourceMappingURL=fee-estimation.js.map