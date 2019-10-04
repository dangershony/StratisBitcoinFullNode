using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models
{
    class ObsidianNetwork : Network
    {
        public ObsidianNetwork()
        {
            this.Base58Prefixes = new byte[2][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (75) }; // ODN
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (75 + 128) };  // ODN
        }
    }
}
