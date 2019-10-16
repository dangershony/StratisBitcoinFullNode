using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
    public class TransactionMetadata : IEquatable<TransactionMetadata>
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public TxType TxType { get; set; }

        public uint256 HashTx { get; set; }

        public Dictionary<string, UtxoMetadata> Received { get; set; }

        public Dictionary<string, UtxoMetadata> Spent { get; set; }
        public Dictionary<string, UtxoMetadata> Destinations { get; set; }

        #region overrides of Equals, GetHashCode, ==, !=

        public override bool Equals(object obj)
        {
            return Equals(obj as TransactionMetadata);
        }

        public bool Equals(TransactionMetadata other)
        {
            return other != null &&
                   this.HashTx == other.HashTx;
        }

        public override int GetHashCode()
        {
            return -1052816746 + EqualityComparer<uint256>.Default.GetHashCode(this.HashTx);
        }

        public static bool operator ==(TransactionMetadata left, TransactionMetadata right)
        {
            return EqualityComparer<TransactionMetadata>.Default.Equals(left, right);
        }

        public static bool operator !=(TransactionMetadata left, TransactionMetadata right)
        {
            return !(left == right);
        }

        #endregion
    }
}