using System;
using System.Collections.Generic;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using Newtonsoft.Json;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    public class KeyAddress
    {
        [JsonProperty(PropertyName = "encryptedPrivateKey")]
        public byte[] EncryptedPrivateKey { get; set; }

        [JsonProperty(PropertyName = "compressedpublickey")]
        public byte[] CompressedPublicKey { get; set; }

        [JsonProperty(PropertyName = "bech32")]
        public string Bech32 { get; set; }

        [JsonProperty(PropertyName = "paymentscriptbytes")]
        public byte[] PaymentScriptBytes { get; set; }

        [JsonProperty(PropertyName = "cointype")]
        public int CoinType { get; set; }

        [JsonProperty(PropertyName = "uniqueIndex")]
        public int UniqueIndex { get; set; }

        [JsonProperty(PropertyName = "properties")]
        public IDictionary<string, string> Properties { get; set; }

        [JsonProperty(PropertyName = "transactions")]
        public List<TransactionData> Transactions { get; set; } = new List<TransactionData>();

        public static KeyAddress CreateWithPrivateKey(byte[] privateKey, string keyEncryptionPassphrase, Func<string, byte[], byte[]> keyEncryption, int coinType, int uniqueIndex, byte witnessVersion, string bech32Prefix)
        {
            var adr = new KeyAddress();
            adr.EncryptedPrivateKey = keyEncryption(keyEncryptionPassphrase, privateKey);

            adr.CoinType = coinType;
            adr.UniqueIndex = uniqueIndex;

            var k = new Key(privateKey);
            adr.CompressedPublicKey = k.PubKey.Compress().ToBytes();

            var hash160 = Hashes.Hash160(adr.CompressedPublicKey).ToBytes();
            var enc = new Bech32Encoder(bech32Prefix);
            adr.Bech32 = enc.Encode(witnessVersion, hash160);

            adr.PaymentScriptBytes = adr.GetPaymentScriptBytes();

            return adr;
        }
    }
}
