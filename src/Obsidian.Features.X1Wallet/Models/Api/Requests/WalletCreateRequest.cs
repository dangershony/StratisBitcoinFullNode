﻿namespace Obsidian.Features.X1Wallet.Models.Api.Requests
{
    public class WalletCreateRequest
    {
        /// <summary>
        /// A password used to encrypt the private keys.
        /// </summary>
        public string Passphrase { get; set; }
       
        /// <summary>
        /// The name of the wallet.
        /// </summary>
        public string WalletName { get; set; }
    }
}
