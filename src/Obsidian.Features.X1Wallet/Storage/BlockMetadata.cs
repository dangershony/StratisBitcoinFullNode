using System.Collections.Generic;
using NBitcoin;

namespace Obsidian.Features.X1Wallet.Storage
{
    public class BlockMetadata
    {
        public uint256 HashBlock { get; set; }

        public HashSet<TransactionMetadata> Transactions { get; set; }
       
    }
}