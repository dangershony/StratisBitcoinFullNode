using System.Collections.Generic;
using System.Linq;

namespace NBitcoin
{
    public class MerkleBlock : IBitcoinSerializable
    {
        public MerkleBlock()
        {

        }
        // Public only for unit testing
        BlockHeader header;

        public BlockHeader Header
        {
            get
            {
                return header;
            }
            set
            {
                header = value;
            }
        }
        PartialMerkleTree _PartialMerkleTree;

        public PartialMerkleTree PartialMerkleTree
        {
            get
            {
                return _PartialMerkleTree;
            }
            set
            {
                _PartialMerkleTree = value;
            }
        }

        // Create from a CBlock, filtering transactions according to filter
        // Note that this will call IsRelevantAndUpdate on the filter for each transaction,
        // thus the filter will likely be modified.
        public MerkleBlock(PowBlock powBlock, BloomFilter filter)
        {
            header = powBlock.Header;

            List<bool> vMatch = new List<bool>();
            List<uint256> vHashes = new List<uint256>();


            for(uint i = 0; i < powBlock.Transactions.Count; i++)
            {
                uint256 hash = powBlock.Transactions[(int)i].GetHash();
                vMatch.Add(filter.IsRelevantAndUpdate(powBlock.Transactions[(int)i]));
                vHashes.Add(hash);
            }

            _PartialMerkleTree = new PartialMerkleTree(vHashes.ToArray(), vMatch.ToArray());
        }

        public MerkleBlock(PowBlock powBlock, uint256[] txIds)
        {
            header = powBlock.Header;

            List<bool> vMatch = new List<bool>();
            List<uint256> vHashes = new List<uint256>();
            for(int i = 0; i < powBlock.Transactions.Count; i++)
            {
                var hash = powBlock.Transactions[i].GetHash();
                vHashes.Add(hash);
                vMatch.Add(txIds.Contains(hash));
            }
            _PartialMerkleTree = new PartialMerkleTree(vHashes.ToArray(), vMatch.ToArray());
        }

        #region IBitcoinSerializable Members

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref header);
            stream.ReadWrite(ref _PartialMerkleTree);
        }

        #endregion
    }
}
