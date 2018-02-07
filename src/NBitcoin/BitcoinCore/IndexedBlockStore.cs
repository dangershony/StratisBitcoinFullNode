#if !NOFILEIO
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NBitcoin.BitcoinCore
{
    public class IndexedBlockStore : IndexedStore<StoredBlock, PowBlock>, IBlockProvider
    {
        private readonly BlockStore _Store;

        public new BlockStore Store
        {
            get
            {
                return _Store;
            }
        }
        public IndexedBlockStore(NoSqlRepository index, BlockStore store)
            : base(index, store)
        {
            _Store = store;
            IndexedLimit = "Last Index Position";
        }

        public BlockHeader GetHeader(uint256 hash)
        {
            return GetHeaderAsync(hash).GetAwaiter().GetResult();
        }

        public async Task<BlockHeader> GetHeaderAsync(uint256 hash)
        {
            var pos = await Index.GetAsync<DiskBlockPos>(hash.ToString()).ConfigureAwait(false);
            if(pos == null)
                return null;
            var stored = _Store.Enumerate(false, new DiskBlockPosRange(pos)).FirstOrDefault();
            if(stored == null)
                return null;
            return stored.Item.Header;
        }

        public PowBlock Get(uint256 id)
        {
            return GetAsync(id).GetAwaiter().GetResult();
        }
        public Task<PowBlock> GetAsync(uint256 id)
        {
            return GetAsync(id.ToString());
        }

        #region IBlockProvider Members

        public PowBlock GetBlock(uint256 id, List<byte[]> searchedData)
        {
            var block = Get(id.ToString());
            if(block == null)
                throw new Exception("Block " + id + " not present in the index");
            return block;
        }

        #endregion

        protected override string GetKey(PowBlock item)
        {
            return item.GetHash().ToString();
        }

        protected override IEnumerable<StoredBlock> EnumerateForIndex(DiskBlockPosRange range)
        {
            return Store.Enumerate(true, range);
        }

        protected override IEnumerable<StoredBlock> EnumerateForGet(DiskBlockPosRange range)
        {
            return Store.Enumerate(false, range);
        }
    }
}
#endif