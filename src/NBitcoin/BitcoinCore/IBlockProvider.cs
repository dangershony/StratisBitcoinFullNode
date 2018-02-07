using System.Collections.Generic;

namespace NBitcoin.BitcoinCore
{
    public interface IBlockProvider
    {
        PowBlock GetBlock(uint256 id, List<byte[]> searchedData);
    }
}
