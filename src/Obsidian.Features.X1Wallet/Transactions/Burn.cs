using NBitcoin;

namespace Obsidian.Features.X1Wallet.Transactions
{
    public class Burn
    {
        public Money Amount { get; set; }
        public string Utf8String { get; internal set; }
    }
}