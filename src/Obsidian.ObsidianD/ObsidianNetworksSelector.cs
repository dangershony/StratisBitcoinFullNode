﻿using NBitcoin;

namespace Obsidian.OxD
{
	static class ObsidianNetworksSelector
    {
	    public static NetworksSelector Obsidian
	    {
		    get
		    {
			    return new NetworksSelector(() => new ObsidianXMain(), () => null, () => null);
		    }
	    }
	}
}
