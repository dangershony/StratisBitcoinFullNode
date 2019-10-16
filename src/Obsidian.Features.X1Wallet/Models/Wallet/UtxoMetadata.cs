using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
    public class UtxoMetadata
    {
        public string Address { get; set; }

        public uint256 HashTx { get; set; }

        public int Index { get; set; }
        public long Satoshis { get; set; }

        public string GetKey()
        {
            return $"{this.HashTx}-{this.Index}";
        }
    }
}