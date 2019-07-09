using NBitcoin;

namespace Obsidian.OxD
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
