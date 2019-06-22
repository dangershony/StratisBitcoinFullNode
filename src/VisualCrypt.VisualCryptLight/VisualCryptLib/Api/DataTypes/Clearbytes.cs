using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class Clearbytes : SecureBytes
	{
		const int MaxClearBytesLength = 1024 * 1024 * 10;

		// Empty messages are not secret, because the lenght of the cipher is always 0 blocks. Allow lenght of 0?
		const int MinClearBytesLength = 1;

		public Clearbytes(byte[] data) : base(data)
		{
			//if (data.Length > MaxClearBytesLength)
			//	throw new ArgumentOutOfRangeException(nameof(data), string.Format("Text too long. Maximum is {0} characters.", MaxClearBytesLength));

			if (data.Length < MinClearBytesLength)
				throw new NotSupportedException(string.Format("Text too short. Minimum is {0} characters.", MinClearBytesLength));
		}
	}
}