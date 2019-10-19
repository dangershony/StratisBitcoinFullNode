using NBitcoin;

namespace Obsidian.Features.X1Wallet.Staking
{
    public class StakingCoin : Coin
    {
        public readonly string Address;
        public readonly int BlockHeight;
        public readonly uint256 BlockHash;
        public readonly byte[] EncryptedPrivateKey;

        public StakingCoin(uint256 fromTxHash, int fromOutputIndex, Money amount, Script scriptPubKey, byte[] encryptedPrivateKey, string address, int blockHeight, uint256 blockHash) : base(fromTxHash, (uint)fromOutputIndex, amount, scriptPubKey)
        {
            this.Address = address;
            this.BlockHeight = blockHeight;
            this.BlockHash = blockHash;
            this.EncryptedPrivateKey = encryptedPrivateKey;
        }
    }
}
