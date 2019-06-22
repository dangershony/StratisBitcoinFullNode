using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class KeyMaterial64 : SecureBytes
	{
		public KeyMaterial64(byte[] data) : base(data)
		{
			// perform datatype-specific validation here
			if (data.Length != 64)
				throw new ArgumentOutOfRangeException("data", "The length must be 64 bytes.");
		}
	}
}