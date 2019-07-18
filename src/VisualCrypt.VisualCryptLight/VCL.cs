using System;
using System.Diagnostics;
using VisualCrypt.VisualCryptLight.VisualCryptLib;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.DataTypes;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Implementations;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Infrastructure;
using VisualCrypt.VisualCryptLight.VisualCryptLib.Api.Interfaces;
using VisualCrypt.VisualCryptLight.VisualCryptLib.ECC;

namespace VisualCrypt.VisualCryptLight
{
    public class VCL
    {
        static IVisualCrypt2Service _visualCrypt2Service;
        static object lockObject = new object();
        public static IVisualCrypt2Service Instance()
        {
            if (_visualCrypt2Service == null)
            {
                lock (lockObject)
                {
                    if (_visualCrypt2Service == null)
                    {
                        _visualCrypt2Service = new VisualCrypt2Service();
                        _visualCrypt2Service.Init(new Platform_NetStandard());
                    }
                }

            }
            return _visualCrypt2Service;
        }

        static ECKeyPair _ecKeyPair;
        public static ECKeyPair ECKeyPair
        {
            get
            {
                if (_ecKeyPair == null)
                {
                    lock (lockObject)
                    {
                        if (_ecKeyPair == null)
                            _ecKeyPair = Instance().GenerateECKeyPair().Result;
                    }
                }
                return _ecKeyPair;
            }
        }

        public static byte[] Decrypt(byte[] cipherV2Bytes, byte[] publicKey, byte[] privateKey, byte[] privateAuthKey)
        {
            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateKey, publicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateAuthKey, publicKey);
            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes, authSecretBytes);

            CipherV2 cipherV2FromClient = VCL.Instance().BinaryDecodeVisualCrypt(cipherV2Bytes, VCL.GetContext()).Result;
            var binaryDecryptResponse = VCL.Instance().BinaryDecrypt(cipherV2FromClient, keyMaterial64, VCL.GetContext());
            if (!binaryDecryptResponse.IsSuccess && binaryDecryptResponse.Error == LocalizableStrings.MsgPasswordError)
                return null;
            return binaryDecryptResponse.Result.GetBytes();
        }

        public static byte[] Encrypt(byte[] plaintextBytes, byte[] publicKey, byte[] privateKey, byte[] privateAuthKey)
        {
            if (plaintextBytes == null)
                throw new ArgumentNullException(nameof(plaintextBytes));
            if (publicKey == null)
                throw new ArgumentNullException(nameof(publicKey));
            if (privateKey == null)
                throw new ArgumentNullException(nameof(privateKey));

            var hashedSharedSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateKey, publicKey);
            var authSecretBytes = VCL.Instance().CalculateAndHashSharedSecret(privateAuthKey, publicKey);
            var keyMaterial64 = ToKeyMaterial64(hashedSharedSecretBytes,authSecretBytes);

            CipherV2 cipher = VCL.Instance().BinaryEncrypt(new Clearbytes(plaintextBytes), keyMaterial64, new RoundsExponent(RoundsExponent.DontMakeRounds), VCL.GetContext()).Result;
            return VCL.Instance().BinaryEncodeVisualCrypt(cipher, VCL.GetContext()).Result;
        }


        public static byte[] EncryptWithPassphrase(string passphrase, byte[] bytesToEncryt)
        {
            var context = GetContext();
            NormalizedPassword normalizedPassword = Instance().NormalizePassword(passphrase).Result;
            KeyMaterial64 passwordDerivedkeyMaterial64 = Instance().HashPassword(normalizedPassword).Result;
            CipherV2 cipherV2 = Instance().BinaryEncrypt(new Clearbytes(bytesToEncryt), passwordDerivedkeyMaterial64,
                new RoundsExponent(RoundsExponent.DontMakeRounds), context).Result;
            var cipherV2Bytes = Instance().BinaryEncodeVisualCrypt(cipherV2, context).Result;
            return cipherV2Bytes;
        }

        public static byte[] DecryptWithPassphrase(string passphrase, byte[] bytesToDecrypt)
        {
            var context = GetContext();
            NormalizedPassword normalizedPassword = Instance().NormalizePassword(passphrase).Result;
            KeyMaterial64 passwordDerivedkeyMaterial64 = Instance().HashPassword(normalizedPassword).Result;
            CipherV2 cipherV2 = Instance().BinaryDecodeVisualCrypt(bytesToDecrypt, context).Result;
            var response = Instance().BinaryDecrypt(cipherV2, passwordDerivedkeyMaterial64, context);
            if (response.IsSuccess)
                return response.Result.GetBytes();
            return null;
        }


        static KeyMaterial64 ToKeyMaterial64(byte[] hashedSharedSecretBytes, byte[] authSecretBytes)
        {
            byte[] dest = new byte[64];
            Buffer.BlockCopy(hashedSharedSecretBytes, 0, dest, 0, 32);
            Buffer.BlockCopy(authSecretBytes, 0, dest, 32, 32);
            return new KeyMaterial64(dest);
        }

        static LongRunningOperationContext GetContext()
        {
            Action<EncryptionProgress> action = progress =>
            {
                Debug.WriteLine(progress.Message);
            };
            return new LongRunningOperation(action, () => { Debug.WriteLine("Done!"); }).Context;
        }


    }
}
