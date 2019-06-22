using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;

namespace VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations
{
    class VisualCryptAPI2Internal
    {
        IPlatform _platform;

        public VisualCryptAPI2Internal(IPlatform platform)
        {
            this._platform = platform;
        }

        public Compressed Compress(Cleartext cleartext)
        {
            Guard.NotNull(cleartext);

            byte[] compressed = Deflate.Compress(cleartext.Text, Encoding.UTF8);

            return new Compressed(compressed);
        }

        public Compressed CompressBytes(Clearbytes clearbytes)
        {
            Guard.NotNull(clearbytes);

            byte[] compressed = Deflate.CompressBytes(clearbytes.GetBytes());

            return new Compressed(compressed);
        }

        public PaddedData ApplyRandomPadding(Compressed compressed)
        {
            Guard.NotNull(compressed);

            int requiredPadding;
            if (compressed.GetBytes().Length % 16 == 0)
                requiredPadding = 0;
            else
                requiredPadding = 16 - compressed.GetBytes().Length % 16;

            if (requiredPadding == 0)
            {
                return new PaddedData(compressed.GetBytes(), new PlaintextPadding(requiredPadding));
            }

            var paddingBytes = this._platform.GenerateRandomBytes(requiredPadding);

            var paddedDataBytes = new byte[compressed.GetBytes().Length + requiredPadding];
            Buffer.BlockCopy(compressed.GetBytes(), 0, paddedDataBytes, 0, compressed.GetBytes().Length);
            Buffer.BlockCopy(paddingBytes, 0, paddedDataBytes, compressed.GetBytes().Length, paddingBytes.Length);

            return new PaddedData(paddedDataBytes, new PlaintextPadding(requiredPadding));
        }


        public void AESEncryptRandomKeyWithInputDerivedKey(InputDerivedKey32 inputDerivedKey, RandomKey32 randomKey, CipherV2 cipherV2, IVCache ivCache,LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { inputDerivedKey, randomKey, cipherV2, context });

            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                Debug.Assert(ivCache == null);
                cipherV2.RandomKeyCipher32 = new RandomKeyCipher32(this._platform.ComputeAESRound(AESDir.Encrypt, cipherV2.IV16.GetBytes(), randomKey.GetBytes(), inputDerivedKey.GetBytes()));
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgEncryptingRandomKey;
                cipherV2.RandomKeyCipher32 = new RandomKeyCipher32(ComputeAESWithRounds(AESDir.Encrypt, cipherV2.IV16, randomKey.GetBytes(), inputDerivedKey.GetBytes(), cipherV2.RoundsExponent.Value,ivCache, context));
            }
        }


        public void AESEncryptMessageWithRandomKey(PaddedData paddedData, RandomKey32 randomKey, CipherV2 cipherV2,IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { paddedData, randomKey, cipherV2, context });

            cipherV2.PlaintextPadding = paddedData.PlaintextPadding;

            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                Debug.Assert(ivCache == null);
                cipherV2.MessageCipher = new MessageCipher(this._platform.ComputeAESRound(AESDir.Encrypt, cipherV2.IV16.GetBytes(), paddedData.GetBytes(), randomKey.GetBytes()));
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgEncryptingMessage;
                cipherV2.MessageCipher = new MessageCipher(ComputeAESWithRounds(AESDir.Encrypt, cipherV2.IV16, paddedData.GetBytes(), randomKey.GetBytes(), cipherV2.RoundsExponent.Value,ivCache, context));
            }
        }


        public void AESEncryptMACWithRandomKey(CipherV2 cipherV2, MAC16 mac, RandomKey32 randomKey, IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { cipherV2, mac, randomKey, context });

            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                Debug.Assert(ivCache == null);
                cipherV2.MACCipher16 = new MACCipher16(this._platform.ComputeAESRound(AESDir.Encrypt, cipherV2.IV16.GetBytes(), mac.GetBytes(), randomKey.GetBytes()));
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgEncryptingMAC;
                cipherV2.MACCipher16 = new MACCipher16(ComputeAESWithRounds(AESDir.Encrypt, cipherV2.IV16, mac.GetBytes(), randomKey.GetBytes(), cipherV2.RoundsExponent.Value,ivCache, context));
            }
        }


        byte[] ComputeAESWithRounds(AESDir aesDir, IV16 iv, byte[] dataBytes, byte[] keyBytes, byte roundsExp, IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { aesDir, iv, dataBytes, keyBytes, roundsExp,ivCache, context });
            var rounds = 1u << roundsExp;
            var roundsToGo = rounds;

            byte[] inputData = dataBytes;
            byte[] aesResult = null;
            while (roundsToGo > 0)
            {
                byte[] currentIV =
                    aesDir == AESDir.Encrypt
                       ? ivCache.IVTable.Item2[roundsToGo - 1]
                       : ivCache.IVTable.Item2[ivCache.IVTable.Item2.Length - roundsToGo];

                aesResult = this._platform.ComputeAESRound(aesDir, currentIV, inputData, keyBytes);
                inputData = aesResult;

				roundsToGo--; // decrement before calculating the percentage or we'll be stuck at 99%

				// START encryptionProgress / Cancellation
				context.CancellationToken.ThrowIfCancellationRequested();
                decimal progressValue = (rounds -roundsToGo) / (decimal)(rounds);
				
                context.EncryptionProgress.Percent = (int)(progressValue * 100m);
                context.EncryptionProgress.Report(context.EncryptionProgress);
                // END encryptionProgress
            }
            return aesResult;
        }

        internal class IVCache
        {
            public Tuple<byte[], byte[][]> IVTable;
        }


        /// <summary>
        /// Purpose:
        /// </summary>
        /// <param name="iv"></param>
        /// <param name="roundsExp"></param>
        /// <returns></returns>
        public IVCache CreateIVTable(IV16 iv, byte roundsExp)
        {
            Guard.NotNull(new object[] { iv });

            var ivCache = new IVCache();
            var rounds = 1u << roundsExp;
            var ivRounds = rounds;
            var ivTable = new byte[rounds][];
            byte[] ivInput = iv.GetBytes();
            while (ivRounds > 0)
            {

                ivTable[ivTable.Length - ivRounds] = ivInput;

                ivInput = this._platform.ComputeSHA256(ivInput).Take(16).ToArray();

                ivRounds = ivRounds - 1;
            }
            ivCache.IVTable= new Tuple<byte[], byte[][]>(iv.GetBytes(), ivTable);
            return ivCache;
        }


        public MAC16 AESDecryptMAC(CipherV2 cipherV2, RandomKey32 randomKey,IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { cipherV2, randomKey, context });
            byte[] macBytes;
            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                macBytes = this._platform.ComputeAESRound(AESDir.Decrpyt, cipherV2.IV16.GetBytes(), cipherV2.MACCipher16.GetBytes(), randomKey.GetBytes());
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgDecryptingMAC;
                macBytes = ComputeAESWithRounds(AESDir.Decrpyt, cipherV2.IV16, cipherV2.MACCipher16.GetBytes(), randomKey.GetBytes(), cipherV2.RoundsExponent.Value, ivCache, context);
            }
            return new MAC16(macBytes);
        }



        public RandomKey32 AESDecryptRandomKeyWithPasswordDerivedKey(CipherV2 cipherV2, InputDerivedKey32 inputDerivedKey,IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { cipherV2, inputDerivedKey, context });

            byte[] randomKeyBytes;
            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                randomKeyBytes = this._platform.ComputeAESRound(AESDir.Decrpyt, cipherV2.IV16.GetBytes(), cipherV2.RandomKeyCipher32.GetBytes(), inputDerivedKey.GetBytes());
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgDecryptingRandomKey;
                randomKeyBytes = ComputeAESWithRounds(AESDir.Decrpyt, cipherV2.IV16, cipherV2.RandomKeyCipher32.GetBytes(), inputDerivedKey.GetBytes(), cipherV2.RoundsExponent.Value,ivCache, context);
            }
            return new RandomKey32(randomKeyBytes);
        }


        public PaddedData AESDecryptMessage(CipherV2 cipherV2, IV16 iv16, RandomKey32 randomKey,IVCache ivCache, LongRunningOperationContext context)
        {
            Guard.NotNull(new object[] { cipherV2, iv16, randomKey, context });

            byte[] paddedDataBytes;
            if (cipherV2.RoundsExponent.Value == RoundsExponent.DontMakeRounds)
            {
                paddedDataBytes = this._platform.ComputeAESRound(AESDir.Decrpyt, cipherV2.IV16.GetBytes(), cipherV2.MessageCipher.GetBytes(), randomKey.GetBytes());
            }
            else
            {
                context.EncryptionProgress.Message = LocalizableStrings.MsgDecryptingMessage;
                paddedDataBytes = ComputeAESWithRounds(AESDir.Decrpyt, cipherV2.IV16, cipherV2.MessageCipher.GetBytes(), randomKey.GetBytes(), cipherV2.RoundsExponent.Value,ivCache, context);
            }
            return new PaddedData(paddedDataBytes, cipherV2.PlaintextPadding);
        }

        public Compressed RemovePadding(PaddedData paddedData)
        {
            Guard.NotNull(paddedData);

            var paddingRemoved = new byte[paddedData.GetBytes().Length - paddedData.PlaintextPadding.Value];

            Buffer.BlockCopy(paddedData.GetBytes(), 0, paddingRemoved, 0, paddingRemoved.Length);

            return new Compressed(paddingRemoved);
        }

        public Cleartext Decompress(Compressed compressed)
        {
            Guard.NotNull(compressed);

            var clearText = Deflate.Decompress(compressed.GetBytes(), Encoding.UTF8);
            return new Cleartext(clearText);
        }

        public Clearbytes DecompressBytes(Compressed compressed)
        {
            Guard.NotNull(compressed);

            var clearBytes = Deflate.DecompressBytes(compressed.GetBytes());
            return new Clearbytes(clearBytes);
        }

        public string GenerateRandomPassword()
        {
            var passwordBytes = this._platform.GenerateRandomBytes(32);

            char[] passwordChars = Base64Encoder.EncodeDataToBase64CharArray(passwordBytes);

            string passwordString = new string(passwordChars).Remove(43).Replace("/", "$");
            var sb = new StringBuilder();

            for (var i = 0; i != passwordString.Length; ++i)
            {
                sb.Append(passwordString[i]);
                var insertSpace = (i + 1) % 5 == 0;
                var insertNewLine = (i + 1) % 25 == 0;
                if (insertNewLine)
                    sb.Append(Environment.NewLine);
                else if (insertSpace)
                    sb.Append(" ");
            }
            return sb.ToString();
        }

        public QualifiedRandom TestRandomNumberGeneration(int sampleSize, int randomLenght)
        {
            checked
            {
                if (randomLenght * 255 > int.MaxValue)
                    throw new ArgumentException(string.Format("The maximum array lenght for this test is {0}", int.MaxValue / 255), "randomLenght");

                var a = randomLenght;

                var X = this._platform.GenerateRandomBytes(a);  // a [0..255]

                var Xa = Sum(X);

                var E_Xa = 256 / 2 * a; // discrete uniform distibution


                return new QualifiedRandom { X = X, a = a, Xa = Xa, E_Xa = E_Xa, k = sampleSize };
            }
        }

        int Sum(byte[] bytes)
        {
            int sum = 0;
            foreach (var b in bytes)
                sum += b;
            return sum;
        }


    }
}

