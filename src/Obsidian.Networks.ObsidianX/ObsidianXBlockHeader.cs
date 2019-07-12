using NBitcoin;

namespace Obsidian.Networks.ObsidianX
{
    public class ObsidianXBlockHeader : PosBlockHeader
    {
	    public override uint256 GetPoWHash()
        {
            var blockHeaderBytes = this.ToBytes();
		    return ObsidianXHash.GetObsidianXPoWHash(blockHeaderBytes);
	    }
    }
}
