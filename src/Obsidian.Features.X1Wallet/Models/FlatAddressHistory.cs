using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    /// <summary>
    /// A class that represents a flat view of the wallets history.
    /// </summary>
    public class FlatAddressHistory
    {
        /// <summary>
        /// The address associated with this UTXO.
        /// </summary>
        public P2WPKHAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }
    }
}
