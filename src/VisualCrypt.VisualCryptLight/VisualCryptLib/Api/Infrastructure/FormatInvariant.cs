using System.Globalization;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
    static class StringFormatInvariant
	{
		public static string FormatInvariant(this string format, params object[] args)
		{
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}
	}
}