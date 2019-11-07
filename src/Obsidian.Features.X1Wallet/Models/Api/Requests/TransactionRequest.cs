using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Api.Requests
{
    public class TransactionRequest
    {
        public string Passphrase;
        public List<Recipient> Recipients;
        public List<Burn> Burns;
        public bool Sign;
        public bool Send;
        public uint? TransactionTimestamp;
    }

    public class Burn
    {
        public long Amount { get; set; }
        public byte[] Data { get; set; }
    }

    public class Recipient
    {
        public string Address { get; set; }

        public long Amount { get; set; }
    }
}