using System;
using System.Text;
using Obsidian.Features.X1Wallet.Storage;

namespace Obsidian.Features.X1Wallet.Models
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
