using NBitcoin;

namespace Obsidian.Features.X1Wallet.Temp
{
    public class Spendable
    {
        public Transaction Transaction;
        public TxOut TxOut;
        public int OutIndex;
    }
}