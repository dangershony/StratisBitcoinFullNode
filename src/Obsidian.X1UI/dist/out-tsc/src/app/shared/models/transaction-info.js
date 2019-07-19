"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var TransactionInfo = /** @class */ (function () {
    function TransactionInfo(transactionType, transactionId, transactionAmount, transactionFee, transactionConfirmedInBlock, transactionTimestamp) {
        this.transactionType = transactionType;
        this.transactionId = transactionId;
        this.transactionAmount = transactionAmount;
        this.transactionFee = transactionFee;
        this.transactionConfirmedInBlock = transactionConfirmedInBlock;
        this.transactionTimestamp = transactionTimestamp;
    }
    return TransactionInfo;
}());
exports.TransactionInfo = TransactionInfo;
//# sourceMappingURL=transaction-info.js.map