using NBitcoin;
using Stratis.Bitcoin.Networks;

namespace Obsidian.DroidD.Node
{
	static class ObsidianNetworksSelector
    {
	    public static NetworksSelector Obsidian
	    {
		    get
		    {
			    return new NetworksSelector(() => new ObsidianMain(), () => null, () => null);
		    }
	    }
	}
}
