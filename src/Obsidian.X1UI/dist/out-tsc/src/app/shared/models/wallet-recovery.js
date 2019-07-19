"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var WalletRecovery = /** @class */ (function () {
    function WalletRecovery(walletName, mnemonic, password, passphrase, creationDate, folderPath) {
        if (folderPath === void 0) { folderPath = null; }
        this.name = walletName;
        this.mnemonic = mnemonic;
        this.password = password;
        this.passphrase = passphrase;
        this.creationDate = creationDate;
        this.folderPath = folderPath;
    }
    return WalletRecovery;
}());
exports.WalletRecovery = WalletRecovery;
//# sourceMappingURL=wallet-recovery.js.map