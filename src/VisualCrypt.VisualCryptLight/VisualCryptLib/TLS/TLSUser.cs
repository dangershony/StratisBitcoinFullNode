using System.Collections.Generic;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.TLS
{
    public class TLSUser
    {
        public TLSUser(string  userId, byte[] staticPublicKey)
        {
            this.UserId = userId;
            this.StaticPublicKey = staticPublicKey;
            this.DynamicPrivateDecryptionKeys = new Dictionary<long, byte[]>();
        }
        public string UserId { get;}
        public byte[] StaticPublicKey { get; }

        public byte[] LatestDynamicPublicKey { get; set; }
        public long LatestDynamicPublicKeyId { get; set; }
        public byte[] AuthSecret { get; set; }
        public DynamicSecret DynamicSecret { get; set; }
        public int PushesDone { get; set; }
        public  Dictionary<long, byte[]> DynamicPrivateDecryptionKeys { get; } 
    }

    
}
