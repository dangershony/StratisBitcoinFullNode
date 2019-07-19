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
var SidechainFeeEstimation = /** @class */ (function () {
    function SidechainFeeEstimation(walletName, accountName, federationAddress, destinationAddress, amount, feeType, allowUnconfirmed) {
        this.walletName = walletName;
        this.accountName = accountName;
        this.recipients = [new Recipient(federationAddress, amount)];
        this.opreturndata = destinationAddress;
        this.feeType = feeType;
        this.allowUnconfirmed = allowUnconfirmed;
    }
    return SidechainFeeEstimation;
}());
exports.SidechainFeeEstimation = SidechainFeeEstimation;
//# sourceMappingURL=sidechain-fee-estimation.js.map