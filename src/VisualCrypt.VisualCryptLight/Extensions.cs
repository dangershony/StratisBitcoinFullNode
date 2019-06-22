using System;
using System.Collections.Generic;
using System.Text;

namespace VisualCrypt.VisualCryptLight
{
	public static class Extensions
	{
		public static string ToBase64(this byte[] bytes)
		{
			return Convert.ToBase64String(bytes);
		}

		public static byte[] ToUTF8Bytes(this string text)
		{
			return Encoding.UTF8.GetBytes(text);
		}

		public static string FromUTF8Bytes(this byte[] utf8Bytes)
		{
			return Encoding.UTF8.GetString(utf8Bytes);
		}
		
	}
}
