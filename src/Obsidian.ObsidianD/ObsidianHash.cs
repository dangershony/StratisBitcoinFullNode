using System;
using System.Security.Cryptography;
using NBitcoin;

namespace Obsidian.ObsidianD
{
	public static class ObsidianHash
    {
	    public static uint256 GetObsidianPoWHash(byte[] blockBytes)
	    {
		    byte[] sha512256 = new byte[32];
		    using (var sha512 = SHA512.Create())
		    {
			    var sha512Full = sha512.ComputeHash(blockBytes);
			    Buffer.BlockCopy(sha512Full, 0, sha512256, 0, 32);
		    }
		    return new uint256(sha512256);
	    }

        public static uint256 GetObsidianXPoWHash(byte[] blockBytes)
        {
            byte[] truncatedDoubleSha512 = new byte[32];
            using (var sha512 = SHA512.Create())
            {
                var sha512Full1 = sha512.ComputeHash(blockBytes);
                var sha512Full2 = sha512.ComputeHash(sha512Full1);
                Buffer.BlockCopy(sha512Full2, 0, truncatedDoubleSha512, 0, 32);
            }
            return new uint256(truncatedDoubleSha512);
        }
    }
}
