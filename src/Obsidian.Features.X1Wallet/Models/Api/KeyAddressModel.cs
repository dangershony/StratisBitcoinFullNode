using Obsidian.Features.X1Wallet.Models.Wallet;

namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class KeyAddressModel
    {
        public string Address { get; set; }
       
        public bool IsUsed { get; set; }
      
        public bool IsChange { get; set; }

        public byte[] EncryptedPrivateKey { get; set; }
        public P2WpkhAddress FullAddress { get; internal set; }
    }
}
