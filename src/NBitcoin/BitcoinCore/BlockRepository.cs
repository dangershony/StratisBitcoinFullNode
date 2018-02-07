#if !NOFILEIO
using System;
using System.Threading.Tasks;

namespace NBitcoin.BitcoinCore
{
    public class BlockRepository : INBitcoinBlockRepository
    {
        IndexedBlockStore _BlockStore;
        IndexedBlockStore _HeaderStore;
        public BlockRepository(IndexedBlockStore blockStore,
                               IndexedBlockStore headerStore)
        {
            if(blockStore == null)
                throw new ArgumentNullException("blockStore");
            if(headerStore == null)
                throw new ArgumentNullException("headerStore");
            if(blockStore == headerStore)
                throw new ArgumentException("The two stores should be different");
            _BlockStore = blockStore;
            _HeaderStore = headerStore;
        }


        public void WriteBlock(PowBlock powBlock)
        {
            WriteBlockHeader(powBlock.Header);
            _BlockStore.Put(powBlock);
        }
        public void WriteBlockHeader(BlockHeader header)
        {
            PowBlock powBlock = new PowBlock(header);
            _HeaderStore.Put(powBlock);
        }

        public PowBlock GetBlock(uint256 hash)
        {
            return _BlockStore.Get(hash) ?? _HeaderStore.Get(hash);
        }

        public async Task<PowBlock> GetBlockAsync(uint256 hash)
        {
            return await _BlockStore.GetAsync(hash).ConfigureAwait(false)
                ?? await _HeaderStore.GetAsync(hash).ConfigureAwait(false);
        }
    }
}
#endif