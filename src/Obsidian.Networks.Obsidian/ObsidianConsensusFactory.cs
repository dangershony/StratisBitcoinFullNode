using NBitcoin;

namespace Obsidian.Networks.Obsidian
{
	public class ObsidianConsensusFactory : PosConsensusFactory
    {
	    public override BlockHeader CreateBlockHeader()
	    {
			return new ObsidianBlockHeader();
		}
    }
}
