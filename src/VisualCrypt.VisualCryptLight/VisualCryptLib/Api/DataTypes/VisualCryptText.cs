using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
	public sealed class VisualCryptText
	{
		/// <summary>
		/// Guaranteed to be non-null.
		/// </summary>
		public string Text
		{
			get { return this._text; }
		}

		readonly string _text;

		public VisualCryptText(string text)
		{
			if (text == null)
				throw new ArgumentNullException("text");

			this._text = text;
		}
	}
}