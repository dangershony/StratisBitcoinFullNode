using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class IV16 : SecureBytes
	{
		public IV16(byte[] data) : base(data)
		{
			// perform datatype-specific validation here
			if (data.Length != 16)
				throw new ArgumentOutOfRangeException("data", "The length must be 16 bytes.");
		}
	}
}