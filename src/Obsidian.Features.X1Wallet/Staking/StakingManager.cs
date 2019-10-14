using System.Collections.Generic;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Obsidian.Features.X1Wallet.Storage;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Staking
{
    public class StakingManager
    {
        readonly WalletManager walletManager;
        readonly string passphrase;
        readonly Dictionary<string, Key> stakingKeys;

        public StakingManager(string passphrase, WalletManager walletManager)
        {
            this.passphrase = passphrase;
            this.walletManager = walletManager;
            this.stakingKeys = new Dictionary<string, Key>();
        }

        public Key GetPrivateKey(string bech32)
        {
            if (this.stakingKeys.TryGetValue(bech32, out Key stakingKey))
                return stakingKey;

            P2WpkhAddress address = this.walletManager.GetAddress(bech32);
            var privateKeyBytes = VCL.DecryptWithPassphrase(this.passphrase, address.EncryptedPrivateKey);
            var key = new Key(privateKeyBytes);
            this.stakingKeys.Add(bech32, key);
            return key;
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            return new List<BuilderExtension>();
        } 

        internal void Dispose()
        {
            // todo: investigate how we can protect keys and passwords in better ways.
            this.stakingKeys.Clear();
        }
    }
}
