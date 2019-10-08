using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Obsidian.Features.X1Wallet
{
    public static class HashStringExtensions
    {
        static string genesisHash;
        static string nullHash;

        public static void Init(Network network)
        {
            genesisHash = network.GenesisHash.ToString();
            nullHash = uint256.Zero.ToString();
        }

        /// <summary>
        /// Checks is the block hash has a nullish value.
        /// </summary>
        /// <param name="hashBlock">block hash</param>
        /// <returns>true, if nullish</returns>
        public static bool IsDefault(this string hashBlock)
        {
            if (string.IsNullOrEmpty(hashBlock) || genesisHash == hashBlock || nullHash == hashBlock)
                return true;
            return false;
        }

        public static uint256 ToUInt256(this string uint256)
        {
            return new uint256(uint256);
        }
    }
}
