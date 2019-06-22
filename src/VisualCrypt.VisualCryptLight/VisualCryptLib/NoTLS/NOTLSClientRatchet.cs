using System.Threading.Tasks;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS
{
	public class NOTLSClientRatchet
	{
		public async Task<NOTLSEnvelope> EncryptRequest(byte[] clearPacket)
		{
			Guard.NotNull(clearPacket);
			return new NOTLSEnvelope(clearPacket, clearPacket.Length);
		}

		public async Task<NOTLSRequest> DecryptRequest(IEnvelope tlsEnvelope)
		{
			Guard.NotNull(tlsEnvelope);
			var ar = new NOTLSRequest
			{
				CommandData = tlsEnvelope.EncipheredPayload,
				IsAuthenticated = false,
				UserId = "Anonym. N."
			};
			return ar;
		}

	

		
	}
}