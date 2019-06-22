using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class BCrypt24 : SecureBytes
	{
		public BCrypt24(byte[] data) : base(data)
		{
			// perform datatype-specific validation here
			if (data.Length != 24)
				throw new ArgumentOutOfRangeException("data", "The length must be 24 bytes.");
		}
	}
}