using System;
using System.Security.Cryptography;
using NBitcoin;

namespace Obsidian.DroidD.Node
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
	}
}
