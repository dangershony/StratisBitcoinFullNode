using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models
{
    /// <summary>
    /// A class that represents the balance of an address.
    /// </summary>
    public class KeyAddressBalance
    {
        /// <summary>
        /// The address for which the balance is calculated.
        /// </summary>
        public P2WPKHAddress KeyAddress { get; set; }

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public Money AmountConfirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public Money AmountUnconfirmed { get; set; }

        /// <summary>
        /// The amount that has enough confirmations to be already spendable.
        /// </summary>
        public Money SpendableAmount { get; set; }
    }
}
