using System.Runtime.Serialization;

namespace Obsidian.Features.SegWitWallet.Web
{
    [DataContract]
    public class RequestObject
    {
        [DataMember(Name = "cipherV2Bytes")]
        public string CipherV2Bytes;

        [DataMember(Name = "currentPublicKey")]
        public string CurrentPublicKey { get; set; }
    }

    [DataContract]
    public class RequestObject<T>
    {
        [DataMember(Name = "cipherV2Bytes")]
        public string CipherV2Bytes;

        [DataMember(Name = "currentPublicKey")]
        public string CurrentPublicKey { get; set; }
    }
   
}
