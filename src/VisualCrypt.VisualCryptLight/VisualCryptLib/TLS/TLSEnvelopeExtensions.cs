using System;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.TLS
{
    public static class TLSEnvelopeExtensions
    {
        public static byte[] Serialize(this TLSEnvelope tlsEnvelope)
        {
            if (tlsEnvelope.TotalLength != TLSEnvelope.HeaderLength + tlsEnvelope.EncipheredPayload.Length)
                throw new InvalidOperationException("Actual payload lenght does not match Length field.");

            int serializedLength = TLSEnvelope.HeaderLength + tlsEnvelope.EncipheredPayload.Length;
            var serialized = new byte[serializedLength];

            serialized[0] = TLSEnvelope.Version;
            serialized[1] = TLSEnvelope.MessageType;

            byte[] lenghtBytes = BitConverter.GetBytes(serializedLength);
            serialized[2] = lenghtBytes[0];
            serialized[3] = lenghtBytes[1];
            serialized[4] = lenghtBytes[2];
            serialized[5] = lenghtBytes[3];

            byte[] keyHint = BitConverter.GetBytes(tlsEnvelope.PrivateKeyHint);
            serialized[6 + 4] = keyHint[0];
            serialized[7 + 4] = keyHint[1];
            serialized[8 + 4] = keyHint[2];
            serialized[9 + 4] = keyHint[3];
            serialized[10 + 4] = keyHint[4];
            serialized[11 + 4] = keyHint[5];
            serialized[12 + 4] = keyHint[6];
            serialized[13 + 4] = keyHint[7];

            byte[] dynamicPublicKeyId = BitConverter.GetBytes(tlsEnvelope.DynamicPublicKeyId);
            serialized[14 + 4] = dynamicPublicKeyId[0];
            serialized[15 + 4] = dynamicPublicKeyId[1];
            serialized[16 + 4] = dynamicPublicKeyId[2];
            serialized[17 + 4] = dynamicPublicKeyId[3];
            serialized[18 + 4] = dynamicPublicKeyId[4];
            serialized[19 + 4] = dynamicPublicKeyId[5];
            serialized[20 + 4] = dynamicPublicKeyId[6];
            serialized[21 + 4] = dynamicPublicKeyId[7];

            Buffer.BlockCopy(tlsEnvelope.DynamicPublicKey, 0, serialized, 22 + 4, 32);

            Buffer.BlockCopy(tlsEnvelope.EncipheredPayload, 0, serialized, TLSEnvelope.HeaderLength, tlsEnvelope.EncipheredPayload.Length);

            var crc32 = Crc32.Compute(serialized);
            byte[] crc32Bytes = BitConverter.GetBytes(crc32);
            serialized[6] = crc32Bytes[0];
            serialized[7] = crc32Bytes[1];
            serialized[8] = crc32Bytes[2];
            serialized[9] = crc32Bytes[3];
            return serialized;
        }








        public static void UpdatePayload(int currentBytesRead, EnvelopeReaderBuffer readerBuffer)
        {
            // read all available Data
            if (readerBuffer.Payload == null) // first read, because readerBuffer.Payload is null
            {
                readerBuffer.Payload = new byte[currentBytesRead];
                Buffer.BlockCopy(readerBuffer.Buffer, 0, readerBuffer.Payload, 0, currentBytesRead);
            }
            else
            {
                var existingLenght = readerBuffer.Payload.Length;
                var newPayload = new byte[existingLenght + currentBytesRead];
                Buffer.BlockCopy(readerBuffer.Payload, 0, newPayload, 0, existingLenght);
                Buffer.BlockCopy(readerBuffer.Buffer, 0, newPayload, 0 + existingLenght, currentBytesRead);
                readerBuffer.Payload = newPayload;
            }
        }


        public static TLSEnvelope TryTakeOnePacket(ref byte[] readerPayload)
        {
            if (readerPayload.Length < TLSEnvelope.HeaderLength)
                return null;

            var advertisedLength = ExtractLenght(readerPayload);
            if (readerPayload.Length < advertisedLength)
                return null;

            // We appear to have enough data to extract one tlsPacket
            byte[] tlsPacket = new byte[advertisedLength];
            Buffer.BlockCopy(readerPayload, 0, tlsPacket, 0, advertisedLength);

            // calculate CRC32 but make no decisions.
            int actualCrc32;
            var crc32Success = ValidateCrc32(tlsPacket, out actualCrc32);


            var remainingLenght = readerPayload.Length - advertisedLength;
            if (remainingLenght == 0)
                readerPayload = null;
            else
            {
                var modifiedReaderPayLoad = new byte[remainingLenght];
                Buffer.BlockCopy(readerPayload, advertisedLength, modifiedReaderPayLoad, 0, modifiedReaderPayLoad.Length);
                readerPayload = modifiedReaderPayLoad;
            }
            return new TLSEnvelope(tlsPacket) { ActualCrc32 = actualCrc32, Crc32Success = crc32Success };
        }

        static int ExtractLenght(byte[] rawRequest)
        {
            return BitConverter.ToInt32(rawRequest, 2);
        }



        public static bool ValidateCrc32(this byte[] tlsPacketBytes, out int actualCrc32)
        {
            int adbvertisedCrc32 = BitConverter.ToInt32(tlsPacketBytes, 6);

            // The bytes for the crc in the message must be zero, as they were in the original calculation.
            tlsPacketBytes[6] = 0;
            tlsPacketBytes[7] = 0;
            tlsPacketBytes[8] = 0;
            tlsPacketBytes[9] = 0;

            // Calculate
            actualCrc32 = Crc32.Compute(tlsPacketBytes);

            // Set tlsPacketBytes back to original state.
            byte[] crc32Bytes = BitConverter.GetBytes(adbvertisedCrc32);
            tlsPacketBytes[6] = crc32Bytes[0];
            tlsPacketBytes[7] = crc32Bytes[1];
            tlsPacketBytes[8] = crc32Bytes[2];
            tlsPacketBytes[9] = crc32Bytes[3];

            return adbvertisedCrc32.Equals(actualCrc32);
        }


    }
}