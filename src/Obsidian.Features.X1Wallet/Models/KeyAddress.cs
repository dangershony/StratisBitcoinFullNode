using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities.JsonConverters;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Models
{
    public class KeyAddressOld
    {
        [JsonProperty(PropertyName = "encryptedPrivateKey")]
        public byte[] EncryptedPrivateKey { get; set; }

        [JsonProperty(PropertyName = "compressedpublickey")]
        public byte[] CompressedPublicKey { get; set; }

        public string Hash160Hex { get; set; }

        [JsonProperty(PropertyName = "bech32")]
        public string Bech32 { get; set; }

        [JsonProperty(PropertyName = "scriptPubKey")]
        [JsonConverter(typeof(ScriptJsonConverter))]
        public Script ScriptPubKey { get; set; }

        [JsonProperty(PropertyName = "cointype")]
        public int CoinType { get; set; }

        [JsonProperty(PropertyName = "label")]
        public string Label { get; set; }

        [JsonProperty(PropertyName = "isChange")]
        public bool IsChange { get; set; }

        [JsonProperty(PropertyName = "createdDateUtc")]
        public DateTime CreatedDateUtc { get; set; }

        [JsonProperty(PropertyName = "transactions")]
        public List<TransactionData> Transactions { get; set; } = new List<TransactionData>();

        public static KeyAddressOld CreateWithPrivateKey(byte[] privateKey, string keyEncryptionPassphrase, Func<string, byte[], byte[]> keyEncryption, int coinType,  byte witnessVersion, string bech32Prefix)
        {
            var adr = new KeyAddressOld();
            adr.EncryptedPrivateKey = keyEncryption(keyEncryptionPassphrase, privateKey);

            adr.CoinType = coinType;

            var k = new Key(privateKey);
            adr.CompressedPublicKey = k.PubKey.Compress().ToBytes();

            var hash160 = Hashes.Hash160(adr.CompressedPublicKey).ToBytes();
            adr.Hash160Hex = hash160.ToHexString();

            var enc = new Bech32Encoder(bech32Prefix);
            adr.Bech32 = enc.Encode(witnessVersion, hash160);
            adr.Label = adr.Bech32;
            adr.ScriptPubKey = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            adr.CreatedDateUtc = DateTime.UtcNow;
            return adr;
        }
    }
}
