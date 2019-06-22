using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class PlaintextPadding
	{
		public byte Value
		{
			get { return this._value; }
		}
		readonly byte _value;

		public PlaintextPadding(int plaintextPadding)
		{
			if (plaintextPadding < 0 || plaintextPadding > 15)
				throw new ArgumentException("The padding amount must be >= 0 and < 16", "plaintextPadding");

			this._value = (byte) plaintextPadding;
		}
	}
}