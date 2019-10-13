using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Obsidian.Features.X1Wallet
{
    public static class HashStringExtensions
    {
        static uint256 genesisHash;
        static uint256 nullHash;

        public static void Init(Network network)
        {
            genesisHash = network.GenesisHash;
            nullHash = uint256.Zero;
        }

        /// <summary>
        /// Checks is the block hash has a nullish value.
        /// </summary>
        /// <param name="hashBlock">block hash</param>
        /// <returns>true, if nullish</returns>
        public static bool IsDefault(this uint256 hashBlock)
        {
            if (hashBlock == null || genesisHash == hashBlock || nullHash == hashBlock)
                return true;
            return false;
        }
       
    }
}
