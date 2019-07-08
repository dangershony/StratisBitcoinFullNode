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

        [DataMember(Name = "isKeyRequest")]
        public bool IsKeyRequest { get; set; }
    }

    public class DecryptedRequest
    {
        public string Command;

        public string Payload;

        public string Target;

    }

    public class DecryptedRequest<T> : DecryptedRequest
    {
        public T Parameter;
    }

    [DataContract]
    public class ResponseObject<T>
    {
        [DataMember(Name = "responsePayload")]
        public T ResponsePayload;
        [DataMember(Name = "status")]
        public int Status;
        [DataMember(Name = "statusText")]
        public string StatusText;
    }

    [DataContract]
    public class RequestObject<T>
    {
        [DataMember(Name = "cipherV2Bytes")]
        public string CipherV2Bytes;

        [DataMember(Name = "currentPublicKey")]
        public string CurrentPublicKey { get; set; }

        [DataMember(Name = "isKeyRequest")]
        public bool IsKeyRequest { get; set; }
    }
   
}
