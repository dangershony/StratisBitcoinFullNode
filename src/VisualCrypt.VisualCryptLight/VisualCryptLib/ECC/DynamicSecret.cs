namespace VisualCrypt.VisualCryptLight.VisualCryptLib.ECC
{
    public sealed class DynamicSecret
    {
        public string RecipientId { get; }
        public byte[] DynamicSharedSecret { get; }
        public byte[] DynamicPublicKey { get; }
        public long DynamicPublicKeyId { get; }
        public long PrivateKeyHint { get; }

        public DynamicSecret(string recipientId, byte[] dynamicSharedSecret, byte[] dynamicPublicKey, long dynamicPublicKeyId, long privateKeyHint)
        {
            this.RecipientId = recipientId;
            this.DynamicSharedSecret = dynamicSharedSecret;
            this.DynamicPublicKey = dynamicPublicKey;
            this.DynamicPublicKeyId = dynamicPublicKeyId;
            this.PrivateKeyHint = privateKeyHint;
        }

        public int UseCount { get; set; }
    }
}