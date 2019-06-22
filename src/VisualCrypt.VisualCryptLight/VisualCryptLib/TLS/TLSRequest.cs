using VisualCrypt.VisualCryptLight.VisualCryptLib.NoTLS;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.TLS
{
    public class TLSRequest : IRequestCommandData
	{
        public string UserId;
        public byte[] CommandData { get; set; }
        public bool IsAuthenticated;
    }
}
