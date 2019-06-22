/*
 
 Copyright (c) 2012-2015 Eugene Larchenko (spct@mail.ru)

 Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

 The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 THE SOFTWARE.
 
*/

using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
    public class Crc32
    {
        const uint kCrcPoly = 0xEDB88320;
        const uint kInitial = 0xFFFFFFFF;
        const int CRC_NUM_TABLES = 8;
        static readonly uint[] Table;

        uint value;

        static Crc32()
        {
            unchecked
            {
                Table = new uint[256*CRC_NUM_TABLES];
                int i;
                for (i = 0; i < 256; i++)
                {
                    var r = (uint) i;
                    for (var j = 0; j < 8; j++)
                        r = (r >> 1) ^ (kCrcPoly & ~((r & 1) - 1));
                    Table[i] = r;
                }
                for (; i < 256*CRC_NUM_TABLES; i++)
                {
                    var r = Table[i - 256];
                    Table[i] = Table[r & 0xFF] ^ (r >> 8);
                }
            }
        }

        public Crc32()
        {
            Init();
        }

        public int Value
        {
            get { return (int) ~this.value; }
        }

        /// <summary>
        ///     Reset CRC
        /// </summary>
        public void Init()
        {
            this.value = kInitial;
        }

        public  void Update(byte[] data, int offset, int count)
        {
            new ArraySegment<byte>(data, offset, count); // check arguments
            if (count == 0) return;

            var table = Table;

            var crc = this.value;

            for (; (offset & 7) != 0 && count != 0; count--)
                crc = (crc >> 8) ^ table[(byte) crc ^ data[offset++]];

            if (count >= 8)
            {
                /*
                 * Idea from 7-zip project sources (http://7-zip.org/sdk.html)
                 */

                var end = (count - 8) & ~7;
                count -= end;
                end += offset;

                while (offset != end)
                {
                    crc ^=
                        (uint)
                            (data[offset] + (data[offset + 1] << 8) + (data[offset + 2] << 16) +
                             (data[offset + 3] << 24));
                    var high =
                        (uint)
                            (data[offset + 4] + (data[offset + 5] << 8) + (data[offset + 6] << 16) +
                             (data[offset + 7] << 24));
                    offset += 8;

                    crc = table[(byte) crc + 0x700]
                          ^ table[(byte) (crc >>= 8) + 0x600]
                          ^ table[(byte) (crc >>= 8) + 0x500]
                          ^ table[ /*(byte)*/(crc >> 8) + 0x400]
                          ^ table[(byte) high + 0x300]
                          ^ table[(byte) (high >>= 8) + 0x200]
                          ^ table[(byte) (high >>= 8) + 0x100]
                          ^ table[ /*(byte)*/(high >> 8) + 0x000];
                }
            }

            while (count-- != 0)
                crc = (crc >> 8) ^ table[(byte) crc ^ data[offset++]];

            this.value = crc;
        }

        public static int Compute(byte[] data, int offset, int count)
        {
            var crc = new Crc32();
            crc.Update(data, offset, count);
            return crc.Value;
        }

        public static int Compute(byte[] data)
        {
            return Compute(data, 0, data.Length);
        }

        public static int Compute(ArraySegment<byte> block)
        {
            return Compute(block.Array, block.Offset, block.Count);
        }
    }
}