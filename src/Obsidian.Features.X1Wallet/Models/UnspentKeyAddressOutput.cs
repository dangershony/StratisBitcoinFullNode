using NBitcoin;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    /// <summary>
    /// Represents an UTXO that keeps a reference to a <see cref="KeyAddress"/>.
    /// </summary>
    public class UnspentKeyAddressOutput
    {

        /// <summary>
        /// The address associated with this UTXO
        /// </summary>
        public P2WpkhAddress Address { get; set; }

        /// <summary>
        /// The transaction representing the UTXO.
        /// </summary>
        public TransactionData Transaction { get; set; }

        /// <summary>
        /// Number of confirmations for this UTXO.
        /// </summary>
        public int Confirmations { get; set; }

        /// <summary>
        /// Convert the <see cref="TransactionData"/> to an <see cref="OutPoint"/>
        /// </summary>
        /// <returns>The corresponding <see cref="OutPoint"/>.</returns>
        public OutPoint ToOutPoint()
        {
            return new OutPoint(this.Transaction.Id, (uint)this.Transaction.Index);
        }
    }
}
