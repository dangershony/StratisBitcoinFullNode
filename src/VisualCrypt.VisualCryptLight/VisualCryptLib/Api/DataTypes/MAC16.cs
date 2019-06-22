using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class MAC16 : SecureBytes
	{
		public MAC16(byte[] data) : base(data)
		{
			// perform datatype-specific validation here
			if (data.Length != 16)
				throw new ArgumentOutOfRangeException("data", "The length must be 16 bytes.");
		}
	}
}