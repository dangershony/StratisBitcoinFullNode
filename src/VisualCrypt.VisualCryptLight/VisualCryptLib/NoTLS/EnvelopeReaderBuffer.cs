using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VisualCrypt.VisualCryptLight.VisualCryptLib.TLS;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS
{
    public class EnvelopeReaderBuffer
    {
        public byte[] Buffer;
        public byte[] Payload;
    }

    public static class TLSEnvelopeReader
    {
        public static async Task<List<IEnvelope>> ReceivePackets(EnvelopeReaderBuffer readerBuffer, Stream stream, CancellationToken ct)
        {
            var receivedPackets = new List<IEnvelope>();

            int currentBytesRead;
            TLSEnvelope packet;
            do
            {
                currentBytesRead = await stream.ReadAsync(readerBuffer.Buffer, 0, readerBuffer.Buffer.Length, ct);
                TLSEnvelopeExtensions.UpdatePayload(currentBytesRead, readerBuffer);
                packet = TLSEnvelopeExtensions.TryTakeOnePacket(ref readerBuffer.Payload);
                if (packet != null)
                    receivedPackets.Add(packet);

            } while (currentBytesRead > 0 && (packet == null || readerBuffer.Payload != null));
            return receivedPackets;
        }
    }
   
}
