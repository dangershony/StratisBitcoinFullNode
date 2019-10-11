using System;
using System.Collections.Generic;
using System.Text;

namespace VisualCrypt.VisualCryptLight
{
    public static class Extensions
    {
        static readonly Dictionary<byte, string> HexTable = new Dictionary<byte, string>();
        static readonly Dictionary<string, byte> HexTable2 = new Dictionary<string, byte>();

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

        public static byte[] FromHexString(this string hexString)
        {
            EnsureHexTable();

            var bytes = new byte[hexString.Length / 2];
            var byteIndex = 0;
            for (var i = 0; i < hexString.Length; i += 2)
            {
                bytes[byteIndex] = HexTable2[hexString.Substring(i, 2)];
                byteIndex++;
            }
            return bytes;
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
                            var hexString = i.ToString("x2");
                            HexTable.Add(i, hexString);
                            HexTable2.Add(hexString, i);
                            if (i == 255)  // overflow!
                                return;
                        }
                    }
                }
            }
        }
    }
}
