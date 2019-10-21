using NBitcoin;

namespace Obsidian.Features.X1Wallet.Transactions
{
    public class BuildTransactionResponse
    {
        public Transaction Transaction;
        public string Hex;
        public long Fee;
        public uint256 TransactionId;
        public int SerializedSize;
        public int VirtualSize;
    }
}
