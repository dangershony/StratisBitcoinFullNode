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
var TransactionBuilding = /** @class */ (function () {
    function TransactionBuilding(walletName, accountName, password, destinationAddress, amount, feeAmount, allowUnconfirmed, shuffleOutputs, opReturnData, opReturnAmount) {
        this.walletName = walletName;
        this.accountName = accountName;
        this.password = password;
        this.recipients = [new Recipient(destinationAddress, amount)];
        this.feeAmount = feeAmount;
        this.allowUnconfirmed = allowUnconfirmed;
        this.shuffleOutputs = shuffleOutputs;
        this.opReturnData = opReturnData;
        this.opReturnAmount = opReturnAmount;
    }
    return TransactionBuilding;
}());
exports.TransactionBuilding = TransactionBuilding;
//# sourceMappingURL=transaction-building.js.map