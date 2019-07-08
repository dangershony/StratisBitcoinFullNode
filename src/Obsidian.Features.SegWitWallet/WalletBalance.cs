﻿using NBitcoin;

namespace Obsidian.Features.SegWitWallet
{
    public class WalletBalance
    {
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
