using System.IO;
using NBitcoin;

namespace Obsidian.Networks.ObsidianX
{
    public class ObsidianXBlockHeader : PosBlockHeader
    {
	    public override uint256 GetPoWHash()
        {
            byte[] serialized;

            using (var ms = new MemoryStream())
            {
                this.ReadWriteHashingStream(new BitcoinStream(ms, true));
                serialized = ms.ToArray();
            }

		    return ObsidianXHash.GetObsidianXPoWHash(serialized);
	    }
    }

    public class ObsidianXProvenBlockHeader : ProvenBlockHeader
    {
        public override uint256 GetPoWHash()
        {
            byte[] serialized;

            using (var ms = new MemoryStream())
            {
                this.ReadWriteHashingStream(new BitcoinStream(ms, true));
                serialized = ms.ToArray();
            }

            return ObsidianXHash.GetObsidianXPoWHash(serialized);
        }
    }
}
