using System.Threading.Tasks;

namespace NBitcoin
{
    public interface INBitcoinBlockRepository
    {
        Task<PowBlock> GetBlockAsync(uint256 blockId);
    }

    public interface IBlockTransactionMapStore
    {
        uint256 GetBlockHash(uint256 trxHash);
    }
}
