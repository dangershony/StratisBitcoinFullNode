"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var WalletCreation = /** @class */ (function () {
    function WalletCreation(name, mnemonic, password, passphrase, folderPath) {
        if (folderPath === void 0) { folderPath = null; }
        this.name = name;
        this.mnemonic = mnemonic;
        this.password = password;
        this.passphrase = passphrase;
        this.folderPath = folderPath;
    }
    return WalletCreation;
}());
exports.WalletCreation = WalletCreation;
//# sourceMappingURL=wallet-creation.js.map