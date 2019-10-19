using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Transactions
{
    public class Recipient
    {
        public Script ScriptPubKey { get; set; }
      
        public Money Amount { get; set; }
       
    }
}
