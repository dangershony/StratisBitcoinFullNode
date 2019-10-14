using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Adapters
{
    public class WalletStakingAdapter : Wallet
    {
        readonly WalletManagerWrapper walletManagerWrapper;
        readonly string walletName;
        readonly Dictionary<string, ISecret> stakingKeys;

        public WalletStakingAdapter(WalletManagerWrapper walletManagerWrapper, string walletName)
        {
            this.walletManagerWrapper = walletManagerWrapper;
            this.walletName = walletName;
            this.stakingKeys = new Dictionary<string, ISecret>();
        }

        public override ISecret GetExtendedPrivateKeyForAddress(string password, HdAddress address)
        {
            var bech32 = address.Bech32Address;

            if (this.stakingKeys.TryGetValue(bech32, out ISecret stakingKey))
                return stakingKey;

            ISecret secret = GetStakingSecret(password, bech32);
            this.stakingKeys.Add(bech32, secret);
            return secret;
        }

        ISecret GetStakingSecret(string password, string bech32)
        {
            byte[] epk;
            using (var context = this.walletManagerWrapper.GetWalletContext(this.walletName))
            {
                epk = context.WalletManager.GetAddress(bech32).EncryptedPrivateKey;
            }

            var privateKeyBytes = VCL.DecryptWithPassphrase(password, epk);
            return new StakingSecret(new Key(privateKeyBytes));
        }
    }
}
