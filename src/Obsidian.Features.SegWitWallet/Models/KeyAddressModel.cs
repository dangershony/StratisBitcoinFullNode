using System;
using System.Collections.Generic;
using System.Text;

namespace Obsidian.Features.X1Wallet.Models
{
    public class KeyAddressesModel
    {
        public IEnumerable<KeyAddressModel> Addresses { get; set; }
    }

    public class KeyAddressModel
    {
        public string Address { get; set; }
       
        public bool IsUsed { get; set; }
      
        public bool IsChange { get; set; }

        public byte[] EncryptedPrivateKey { get; set; }
    }
}
