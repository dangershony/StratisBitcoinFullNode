using System;
using VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.TLS
{
    /// <summary>
    /// Format Specification for a VisualCrypt TLS datagram.
    /// </summary>
    /// <seealso cref="http://blog.fourthbit.com/2014/12/23/traffic-analysis-of-an-ssl-slash-tls-session"/>
    public class TLSEnvelope : IEnvelope
    {
        public const int HeaderLength = 58;  // incl. Crc32
		public const byte Version = 0x01;
	    public const byte MessageType = 0x01;
		public int TotalLength { get; private set; }
       
        public byte[] EncipheredPayload { get; private set; }
		public  int Crc32 { get; private set; }

		public int? ActualCrc32;
        public bool? Crc32Success;

	    public readonly long PrivateKeyHint;
	    public readonly long DynamicPublicKeyId;
	    public readonly byte[] DynamicPublicKey;

		public TLSEnvelope(byte[] rawRequest)
        {
            // index 0
            if (rawRequest[0] != Version)
                ThrowValidationError("Version must be 1.");

            // index 1
            if (rawRequest[1] != MessageType)
                ThrowValidationError("MessageType must be 1.");

            // index 2, 3, 4, 5,
            this.TotalLength = BitConverter.ToInt32(rawRequest, 2);
            if (this.TotalLength != rawRequest.Length)
                ThrowValidationError($"Invalid Size: Coded in rawRequest[2,3,4,5]: {this.TotalLength}, Actual: {rawRequest.Length} bytes.");

            // index 6, 7, 8, 9, Crc32, calculated on serialization
            this.Crc32 = BitConverter.ToInt32(rawRequest, 6);

            // index 10, 11, 12, 13, 14, 15, 16, 17
            this.PrivateKeyHint = BitConverter.ToInt64(rawRequest, 10);

            // index 18, 19, 20, 21, 22, 23, 24, 25
            this.DynamicPublicKeyId = BitConverter.ToInt64(rawRequest, 18);

            // index 26- 57
            this.DynamicPublicKey = new byte[32];
            Buffer.BlockCopy(rawRequest, 26, this.DynamicPublicKey, 0, 32);

            // index 58 (= HeaderLength) - EOF
            this.EncipheredPayload = new byte[rawRequest.Length - HeaderLength];
            Buffer.BlockCopy(rawRequest, HeaderLength, this.EncipheredPayload, 0, this.EncipheredPayload.Length);
        }

        public TLSEnvelope(long privateKeyHint, long dynamicPublicKeyId, byte[] dynamicPublickey, byte[] encipheredPayload)
        {
            this.DynamicPublicKey = dynamicPublickey;
            this.EncipheredPayload = encipheredPayload;
            this.PrivateKeyHint = privateKeyHint;
            this.DynamicPublicKeyId = dynamicPublicKeyId;
            this.TotalLength = HeaderLength + encipheredPayload.Length;
        }


        static void ThrowValidationError(string error)
        {
            throw new Exception($"{nameof(TLSEnvelope)}: {error}");
        }
    }
}
