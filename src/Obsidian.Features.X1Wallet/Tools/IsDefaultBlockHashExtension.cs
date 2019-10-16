using NBitcoin;

namespace Obsidian.Features.X1Wallet.Tools
{
    public static class IsDefaultBlockHashExtension
    {
        static uint256 _genesisHash;
        static uint256 _nullHash;

        public static void Init(Network network)
        {
            _genesisHash = network.GenesisHash;
            _nullHash = uint256.Zero;
        }

        /// <summary>
        /// Checks is the block hash has a nullish value.
        /// </summary>
        /// <param name="hashBlock">block hash</param>
        /// <returns>true, if nullish</returns>
        public static bool IsDefaultBlockHash(this uint256 hashBlock)
        {
            if (hashBlock == null || _genesisHash == hashBlock || _nullHash == hashBlock)
                return true;
            return false;
        }
       
    }
}
