using System;
using System.IO;
using System.Security.Cryptography;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib
{
    public class Platform_NetStandard : IPlatform
    {
        public byte[] GenerateRandomBytes(int length)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException("length");

            var randomBytes = new byte[length];
			
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
                return randomBytes;
            }
        }

        public byte[] ComputeAESRound(AESDir aesDir, byte[] iv, byte[] input, byte[] keyBytes)
        {
            Guard.NotNull(new object[] { iv, input, keyBytes });

            switch (aesDir)
            {
                case AESDir.Encrypt:
                    using (var aes = CreateAesManaged(iv, keyBytes))
                    using (var stream = new MemoryStream())
                    {
                        using (var encryptor = aes.CreateEncryptor())
                        using (var encrypt = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                        {
                            encrypt.Write(input, 0, input.Length);
                            encrypt.FlushFinalBlock();
                        }
                        return stream.ToArray();
                    }
                case AESDir.Decrpyt:
                    using (var aes = CreateAesManaged(iv, keyBytes))
                    using (var stream = new MemoryStream())
                    {
                        using (var decryptor = aes.CreateDecryptor())
                        using (var decrypt = new CryptoStream(stream, decryptor, CryptoStreamMode.Write))
                        {
                            decrypt.Write(input, 0, input.Length);
                            decrypt.FlushFinalBlock();
                        }
                        return stream.ToArray();
                    }
            }
            throw new InvalidOperationException(string.Format("{0} is not supported", aesDir));
        }

        public byte[] ComputeSHA512(byte[] data)
        {
            Guard.NotNull(data);

            using (var sha = SHA512.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        public byte[] ComputeSHA256(byte[] data)
        {
            Guard.NotNull(data);

            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(data);
            }
        }

        static Aes CreateAesManaged(byte[] iv, byte[] keyBytes)
        {
            Guard.NotNull(new object[] { iv, keyBytes });

	        var aes = Aes.Create();
	        aes.KeySize = 256;
	        aes.BlockSize = 128;
			aes.Padding = PaddingMode.None;
	        aes.IV = iv;
	        aes.Key = keyBytes;
			aes.Mode = CipherMode.CBC;
	        return aes;

        }

	   

      

       

    }
}