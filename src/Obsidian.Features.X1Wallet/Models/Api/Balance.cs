namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class Balance
    {
        

        /// <summary>
        /// The balance of confirmed transactions.
        /// </summary>
        public long Confirmed { get; set; }

        /// <summary>
        /// The balance of unconfirmed transactions.
        /// </summary>
        public long Pending { get; set; }

        /// <summary>
        /// The amount that has enough confirmations to be already spendable.
        /// </summary>
        public long Spendable { get; set; }

        public long Stakable;
    }
}
