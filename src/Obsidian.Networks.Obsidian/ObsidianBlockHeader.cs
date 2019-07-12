using NBitcoin;

namespace Obsidian.Networks.Obsidian
{
    public class ObsidianBlockHeader : PosBlockHeader
    {
	    public override uint256 GetPoWHash()
	    {
		    var blockHeaderBytes = this.ToBytes();
		    return ObsidianHash.GetObsidianPoWHash(blockHeaderBytes);
	    }
    }
}
