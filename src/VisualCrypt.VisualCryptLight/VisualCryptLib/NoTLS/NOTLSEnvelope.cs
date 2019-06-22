using System;
using System.Diagnostics;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS
{
    /// <summary>
    /// Format Specification for a VisualCrypt TLS datagram.
    /// </summary>
    /// <seealso cref="http://blog.fourthbit.com/2014/12/23/traffic-analysis-of-an-ssl-slash-tls-session"/>
    public class NOTLSEnvelope : IEnvelope
	{
        public const int HeaderLength = 10;  // incl. Crc32
        public const byte Version = 0x01;
        public const byte MessageType = 0x02; // TLSEnvelope is version 0x01
        public readonly int TotalLength;
     
        public byte[] EncipheredPayload { get; private set; }
        public readonly int Crc32;
        public int? ActualCrc32;
        public bool? Crc32Success;

		/// <summary>
		/// For receiving.
		/// </summary>
		/// <param name="rawRequest"></param>
        public NOTLSEnvelope(byte[] rawRequest)
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

            // index 10 (= HeaderLength) - EOF
            this.EncipheredPayload = new byte[rawRequest.Length - HeaderLength];
            Buffer.BlockCopy(rawRequest, HeaderLength, this.EncipheredPayload, 0, this.EncipheredPayload.Length);
        }

		/// <summary>
		/// For sending.
		/// </summary>
		/// <param name="encipheredPayload"></param>
		/// <param name="length"></param>
        public NOTLSEnvelope(byte[] encipheredPayload, int length)
        {
            this.EncipheredPayload = encipheredPayload;
			Debug.Assert(length == encipheredPayload.Length);
            this.TotalLength = HeaderLength + encipheredPayload.Length;
        }


        static void ThrowValidationError(string error)
        {
            throw new Exception($"{nameof(NOTLSEnvelope)}: {error}");
        }
    }
}
