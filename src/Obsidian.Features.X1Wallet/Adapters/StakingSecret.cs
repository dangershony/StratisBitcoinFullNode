using NBitcoin;

namespace Obsidian.Features.X1Wallet.Adapters
{
    public class StakingSecret : ISecret
    {
        public StakingSecret(Key key)
        {
            this.PrivateKey = key;
        }

        public Key PrivateKey { get; }
    }
}
