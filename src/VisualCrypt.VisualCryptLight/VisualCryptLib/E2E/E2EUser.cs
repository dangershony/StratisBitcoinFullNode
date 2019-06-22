using System.Collections.Generic;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.E2E
{
    public class E2EUser
    {
        // No serialization!
        public string UserId { get; set; }
        // No serialization!
        public byte[] StaticPublicKey { get; set; }
	    // No serialization!
		public bool IsJustInitialized { get; set; }

        public byte[] LatestDynamicPublicKey { get; set; }
        public long LatestDynamicPublicKeyId { get; set; }
        public byte[] AuthSecret { get; set; }
        public Dictionary<long, byte[]> DynamicPrivateDecryptionKeys { get; set; }
    }
}
