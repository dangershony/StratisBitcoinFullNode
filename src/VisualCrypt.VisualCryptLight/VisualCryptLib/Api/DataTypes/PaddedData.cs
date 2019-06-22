using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class PaddedData : SecureBytes
	{
		public PlaintextPadding PlaintextPadding
		{
			get { return this._plaintextPadding; }
		}

		readonly PlaintextPadding _plaintextPadding;

		public PaddedData(byte[] data, PlaintextPadding plaintextPadding)
			: base(data)
		{
			// perform datatype-specific validation here
			if (plaintextPadding == null)
				throw new ArgumentNullException("plaintextPadding");

			this._plaintextPadding = plaintextPadding;
		}
	}
}