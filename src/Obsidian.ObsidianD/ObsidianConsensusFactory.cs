using NBitcoin;

namespace Obsidian.ObsidianD
{
	public class ObsidianConsensusFactory : PosConsensusFactory
    {
	    public override BlockHeader CreateBlockHeader()
	    {
			return new ObsidianBlockHeader();
		}
    }
}
