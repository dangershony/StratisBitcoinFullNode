using System;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Wallet;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Tools
{
    public static class AddressHelper
    {
        static Bech32Encoder _encoder;
        static string _hrp1;

        public static void Init(Network network)
        {
            _encoder = network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS];
            _hrp1 = Encoding.ASCII.GetString(_encoder.HumanReadablePart) + "1";
        }

        public static P2WpkhAddress CreateWithPrivateKey(byte[] privateKey, string keyEncryptionPassphrase, Func<string, byte[], byte[]> keyEncryption)
        {
            if (string.IsNullOrWhiteSpace(keyEncryptionPassphrase) || keyEncryption == null)
                throw new ArgumentException(nameof(CreateWithPrivateKey));

            CheckBytes(privateKey, 32);

            var adr = new P2WpkhAddress();
            adr.EncryptedPrivateKey = keyEncryption(keyEncryptionPassphrase, privateKey);

            var k = new Key(privateKey);
            adr.CompressedPublicKey = k.PubKey.Compress().ToBytes();
            var hash160 = Hashes.Hash160(adr.CompressedPublicKey).ToBytes();
            adr.Address = _encoder.Encode(0, hash160);

            return adr;
        }



        public static Script ScriptPubKeyFromPublicKey(this P2WpkhAddress address)
        {
            CheckBytes(address.CompressedPublicKey, 33);
            var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
            return new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
        }

        public static Script ScriptPubKeyFromBech32Safe(this string p2WpkhAddress)
        {
            if (p2WpkhAddress == null || !p2WpkhAddress.StartsWith(_hrp1))
                InvalidAddress(p2WpkhAddress);

            var hash160 = _encoder.Decode(p2WpkhAddress, out var witnessVersion);
            CheckBytes(hash160, 20);

            if (witnessVersion != 0)
                InvalidAddress(p2WpkhAddress);

            return new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
        }

        public static string Bech32P2WpkhFromHash160(this byte[] hash160)
        {
            CheckBytes(hash160, 20);

            return _encoder.Encode(0, hash160);
        }

        static void InvalidAddress(string input, Exception innerException = null)
        {
            var message = $"Invalid address '{input ?? "null"}'.";
            throw new X1WalletException(System.Net.HttpStatusCode.BadRequest, message, innerException);
        }

        static void CheckBytes(byte[] bytes, int expectedLength)
        {
            if (bytes == null || bytes.Length != expectedLength || bytes.All(b => b == bytes[0]))
            {
                var display = bytes == null ? "null" : bytes.ToHexString();
                var message = $"Suspicious byte array '{display}', it does not look like a cryptographic key or hash, please investigate. Expected lenght was {expectedLength}.";
                throw new X1WalletException(System.Net.HttpStatusCode.BadRequest, message, null);
            }
        }

    }
}
