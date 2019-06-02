using NBitcoin;

namespace Obsidian.ObsidianD
{
    public class ObsidianBlockHeader : PosBlockHeader
    {
	    public override uint256 GetPoWHash()
        {
            var blockHeaderBytes = this.ToBytes();
		    return ObsidianHash.GetObsidianXPoWHash(blockHeaderBytes);
	    }
    }
}
