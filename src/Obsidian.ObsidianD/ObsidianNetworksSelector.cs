using NBitcoin;

namespace Obsidian.ObsidianD
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
