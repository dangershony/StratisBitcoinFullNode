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
                Bech32Address = address.Address
            };
            return hd;
        }
    }
}
