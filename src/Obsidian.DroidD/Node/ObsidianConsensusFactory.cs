using NBitcoin;

namespace Obsidian.DroidD.Node
{
	public class ObsidianConsensusFactory : PosConsensusFactory
    {
	    public override BlockHeader CreateBlockHeader()
	    {
			return new ObsidianBlockHeader();
		}
    }
}
