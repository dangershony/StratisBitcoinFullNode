using System;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure
{
    public static class ByteArrays
    {
        public static byte[] Concatenate(params byte[][] byteArrays)
        {
            if (byteArrays == null)
                throw new ArgumentNullException(nameof(byteArrays));

            var retLenght = 0;
            foreach (var byteArray in byteArrays)
            {
                if (byteArray == null)
                    throw new ArgumentNullException(nameof(byteArrays), "One of the byteArrays to be concatenated is null");
                retLenght += byteArray.Length;
            }

            var ret = new byte[retLenght];

            var offset = 0;
            foreach (var byteArray in byteArrays)
            {
                Buffer.BlockCopy(byteArray, 0, ret, offset, byteArray.Length);
                offset += byteArray.Length;
            }
            return ret;
        }

        public static bool AreAllBytesEqualOrBothNull(byte[] arr1, byte[] arr2)
        {
            if (arr1 == null && arr2 == null)
                return true;
            return AreAllBytesEqual(arr1, arr2);
        }

        public static bool AreAllBytesEqual(byte[] arr1, byte[] arr2)
        {
            if (arr1.Length != arr2.Length)
                return false;

            for (var i = 0; i < arr1.Length; i++)
            {
                if (arr1[i] != arr2[i])
                    return false;
            }
            return true;
        }

	    public static bool AreAllBytesZero(byte[] arr1)
	    {
		    int zeroCount = 0;
		    for (var i = 0; i < arr1.Length; i++)
		    {
			    if (arr1[i] == 0)
				    zeroCount++;
		    }
			return zeroCount == arr1.Length;
	    }

		public static bool AreBytesEqual(byte[] arr1, byte[] arr2, int startIndex, int length)
        {
            for (var i = startIndex; i < startIndex + length; i++)
            {
                if (arr1[i] != arr2[i])
                    return false;
            }
            return true;
        }
    }
}