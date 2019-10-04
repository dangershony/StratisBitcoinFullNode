using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Models
{
    public class P2WPKHAddress : IEquatable<P2WPKHAddress>
    {
        /// <summary>
        /// Decrypts to the pure key, length 32 bytes.
        /// </summary>
        public byte[] EncryptedPrivateKey { get; set; }

        /// <summary>
        /// The compressed public key corresponding to the private key, length: 33 bytes.
        /// </summary>
        public byte[] CompressedPublicKey { get; set; }

        /// <summary>
        /// The hash160 of the compressed public key, encoded as hex string. 
        /// Unique Key of this address.
        /// </summary>
        public string HashHex { get; set; }

        /// <summary>
        /// The string representation of the address.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Value indicating if this address was created to be a change address. 
        /// The code should allow the user to edit this flag manually without losing access to coins.
        /// </summary>
        public bool IsChange { get; set; }

        /// <summary>
        /// The BIP-0044 Coin Type or 0 if not defined.
        /// </summary>
        public int CoinType { get; set; }

        /// <summary>
        /// A string to indentify the network, e.g. ODX, tODX, BTC, tBTC.
        /// </summary>
        public string CoinTicker { get; set; }


        public static P2WPKHAddress CreateWithPrivateKey(byte[] privateKey, string keyEncryptionPassphrase, Func<string, byte[], byte[]> keyEncryption, bool isChange, int coinType, byte witnessVersion, string bech32Prefix, string coinTicker)
        {
            if (string.IsNullOrWhiteSpace(keyEncryptionPassphrase) || keyEncryption == null || string.IsNullOrWhiteSpace(bech32Prefix) || string.IsNullOrWhiteSpace(coinTicker))
                throw new ArgumentException(nameof(CreateWithPrivateKey));

            if (privateKey == null || privateKey.Length != 32 || privateKey.All(bytes => bytes == privateKey[0]))
                throw new ArgumentException(nameof(privateKey));

            var adr = new P2WPKHAddress();
            adr.EncryptedPrivateKey = keyEncryption(keyEncryptionPassphrase, privateKey);

            adr.IsChange = isChange;
            adr.CoinType = coinType;
            adr.CoinTicker = coinTicker;

            var k = new Key(privateKey);
            adr.CompressedPublicKey = k.PubKey.Compress().ToBytes();
            var hash160 = Hashes.Hash160(adr.CompressedPublicKey).ToBytes();
            var enc = new Bech32Encoder(bech32Prefix);
            adr.Address = enc.Encode(witnessVersion, hash160);
            adr.HashHex = hash160.ToHexString();

            return adr;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as P2WPKHAddress);
        }

        public bool Equals(P2WPKHAddress other)
        {
            return other != null &&
                   this.HashHex == other.HashHex;
        }

        public override int GetHashCode()
        {
            return -1052816746 + EqualityComparer<string>.Default.GetHashCode(this.HashHex);
        }

        public static bool operator ==(P2WPKHAddress left, P2WPKHAddress right)
        {
            return EqualityComparer<P2WPKHAddress>.Default.Equals(left, right);
        }

        public static bool operator !=(P2WPKHAddress left, P2WPKHAddress right)
        {
            return !(left == right);
        }
    }
}
