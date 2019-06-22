using System;
using System.Runtime.InteropServices;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes
{
    public class SecureBytes
    {
        /// <summary>
        /// Returns a non-null clone of the array stored in the data type. 
        /// </summary>
        public byte[] GetBytes()
        {
            var cloned = new byte[this._data.Length];
            Buffer.BlockCopy(this._data, 0, cloned, 0, this._data.Length);
            return cloned;
        }

        readonly byte[] _data;
        readonly GCHandle _gcHandle;

        protected SecureBytes(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            var nonZeroBytesPresent = false;
            foreach (var b in data)
            {
                if (b != 0)
                {
                    nonZeroBytesPresent = true;
                    break;
                }
            }

            if (data.Length > 0 && !nonZeroBytesPresent)
                throw new ArgumentException("Invalid data: all bytes zero.", "data");

            this._data = data;
            this._gcHandle = GCHandle.Alloc(this._data, GCHandleType.Pinned);
        }

        ~SecureBytes()
        {
            try
            {
                if (this._data != null)
                    this._data.FillWithZeros();
                if (this._gcHandle.IsAllocated)
                    this._gcHandle.Free();
            }
            catch (InvalidOperationException)
            { }
        }
    }
}