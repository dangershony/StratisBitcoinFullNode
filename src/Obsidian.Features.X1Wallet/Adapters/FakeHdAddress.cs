using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Adapters
{
    public static class FakeHdAddress
    {
        public static HdAddress ToFakeHdAddress(this P2WpkhAddress keyAddress)
        {
            var hd = new HdAddress
            {
                Address = keyAddress.Address,
                HdPath = HdOperations.CreateHdPath(keyAddress.CoinType, 0, keyAddress.IsChange, keyAddress.GetHashCode()),
                Index = keyAddress.GetHashCode(),
            };
            return hd;
        }
    }
}
