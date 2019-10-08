using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Adapters
{
    public class WalletStakingAdapter : Wallet
    {
        readonly WalletManagerWrapper walletManagerWrapper;
        readonly string walletName;
        public WalletStakingAdapter(WalletManagerWrapper walletManagerWrapper, string walletName)
        {
            this.walletManagerWrapper = walletManagerWrapper;
            this.walletName = walletName;
        }

        public override ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            var key =  GetKey(password, address);
            return new BitcoinSecret(key, this.walletManagerWrapper.network);
        }


        Key GetKey(string password, HdAddress hdAddress)
        {
            byte[] epk;
            using (var context2 = this.walletManagerWrapper.GetWalletContext(this.walletName))
            {
                epk = context2.WalletManager.GetAddress(hdAddress.Address).EncryptedPrivateKey;
            }
            var privateKeyBytes = VCL.DecryptWithPassphrase(password, epk);
            var key = new Key(privateKeyBytes);
            return key;
        }
    }
}
