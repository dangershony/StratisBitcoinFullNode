using System.Runtime.Serialization;

namespace VisualCrypt.VisualCryptLight
{
	[DataContract]
	public class VCLModel
	{
		[DataMember(Name = "cipherV2Bytes")]
		public string CipherV2Bytes;

		[DataMember(Name = "currentPublicKey")]
		public string CurrentPublicKey { get; set; }
	}
}