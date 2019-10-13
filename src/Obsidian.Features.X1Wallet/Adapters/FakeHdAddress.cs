using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Adapters
{
    public static class FakeHdAddress
    {
        public static HdAddress ToFakeHdAddress(this P2WpkhAddress address)
        {
            var hd = new HdAddress
            {
                Address = address.Address,
                HdPath = HdOperations.CreateHdPath(555, 0, false, address.GetHashCode()),
                Index = address.GetHashCode(),
            };
            return hd;
        }
    }
}
