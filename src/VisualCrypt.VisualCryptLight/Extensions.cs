using System;
using System.Collections.Generic;
using System.Text;

namespace VisualCrypt.VisualCryptLight
{
    public static class Extensions
    {
        static readonly Dictionary<byte, string> HexTable = new Dictionary<byte, string>();
        static readonly object lockObject = new object();

        public static string ToBase64(this byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        public static byte[] FromBase64(this string base64)
        {
            return Convert.FromBase64String(base64);
        }

        public static byte[] ToUTF8Bytes(this string text)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        public static string FromUTF8Bytes(this byte[] utf8Bytes)
        {
            return Encoding.UTF8.GetString(utf8Bytes);
        }

        public static string ToHexString(this byte[] bytes)
        {
            EnsureHexTable();

            var hexString = "";
            foreach (byte b in bytes)
                hexString += HexTable[b];
            return hexString;
        }

        static void EnsureHexTable()
        {
            if (HexTable.Count == 0)
            {
                lock (lockObject)
                {
                    if (HexTable.Count == 0)
                    {
                        for (byte i = 0; i <= 255; i++)
                        {
                            HexTable.Add(i, i.ToString("x2"));
                            if (i == 255)  // overflow!
                                return;
                        }
                    }
                }
            }
        }
    }
}
