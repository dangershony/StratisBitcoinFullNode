using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Transactions
{
    public class BuildTransactionRequest
    {
        public string Passphrase;
        public List<Recipient> Recipients;
        public List<Burn> Burns;
        public bool Sign;
        internal uint? TransactionTimestamp;
    }
}