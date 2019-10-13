using System;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;

namespace Obsidian.Features.X1Wallet.Storage
{
    public static class AddressHelper
    {
        static Network _network;
        static Bech32Encoder _encoder;
        public static void Init(Network network)
        {
            _network = network;
            _encoder = network.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS];
        }


        public static P2WpkhAddress CreateWithPrivateKey(byte[] privateKey, string keyEncryptionPassphrase, Func<string, byte[], byte[]> keyEncryption)
        {
            if (string.IsNullOrWhiteSpace(keyEncryptionPassphrase) || keyEncryption == null)
                throw new ArgumentException(nameof(CreateWithPrivateKey));

            if (privateKey == null || privateKey.Length != 32 || privateKey.All(bytes => bytes == privateKey[0]))
                throw new ArgumentException(nameof(privateKey));

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
            try
            {
                var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
                return new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            }
            catch (Exception)
            {
                return null;
            }

        }

        public static Script ScriptPubKeyFromBech32(this string p2WpkhAddress)
        {
            try
            {
                var hash160 = _encoder.Decode(p2WpkhAddress, out var witnessVersion);
                return new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Bech32P2WpkhFromHash160(this byte[] hash160)
        {
            if (hash160 == null || hash160.Length != 20)
                throw new ArgumentException(nameof(hash160));

            return _encoder.Encode(0, hash160);
        }

    }
}
