using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    public class HistoryInfo
    {
        public List<BlockInfo> Blocks;

        public class BlockInfo
        {
            public int Height;
            public uint Time;
            public string HashBlock;
            public List<TransactionInfo> Transactions;
        }

        public class TransactionInfo
        {
            public long TotalSpent;
            public long TotalReceived;
            public long ValueAdded;
            [JsonConverter(typeof(StringEnumConverter))]
            public TxType TxType;
            public Recipient[] Recipients;
            public Burn[] Burns;
            public string HashTx;
        }
    }
}
