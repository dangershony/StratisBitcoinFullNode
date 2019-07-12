using NBitcoin;

namespace Obsidian.Networks.Obsidian
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

        public static bool IsTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("test");
        }

        public static bool IsRegTest(this Network network)
        {
            return network.Name.ToLowerInvariant().Contains("regtest");
        }
    }
}
