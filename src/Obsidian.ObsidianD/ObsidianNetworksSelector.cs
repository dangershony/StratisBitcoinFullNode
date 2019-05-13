using NBitcoin;
using Stratis.Bitcoin.Networks;

namespace Obsidian.ObsidianD
{
	static class ObsidianNetworksSelector
    {
	    public static NetworksSelector Obsidian
	    {
		    get
		    {
			    return new NetworksSelector(() => new ObsidianMain(), () => new StratisTest(), () => new StratisRegTest());
		    }
	    }
	}
}
