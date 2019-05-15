using NBitcoin;

namespace Obsidian.DroidD.Node
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
