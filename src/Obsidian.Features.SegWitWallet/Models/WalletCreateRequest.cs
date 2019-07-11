using System;
using System.Collections.Generic;
using System.Text;

namespace Obsidian.Features.X1Wallet.Models
{
    public class WalletCreateRequest
    {
        /// <summary>
        /// A password used to encrypt the private keys.
        /// </summary>
        public string Password { get; set; }
       
        /// <summary>
        /// The name of the wallet.
        /// </summary>
        public string Name { get; set; }
    }
}
