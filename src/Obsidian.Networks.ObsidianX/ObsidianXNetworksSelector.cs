using NBitcoin;

namespace Obsidian.Networks.ObsidianX
{
	public static class ObsidianXNetworksSelector
    {
	    public static NetworksSelector Obsidian
	    {
		    get
		    {
			    return new NetworksSelector(() => new ObsidianXMain(), () => null, () => null);
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
