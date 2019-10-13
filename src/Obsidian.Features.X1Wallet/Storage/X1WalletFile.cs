using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.Features.X1Wallet.Storage
{
    public class X1WalletFile
    {
        public const string FileExtension = ".x1wallet.json";

        /// <summary>
        /// The key is the HashHex of an address. The value contains the transaction data for that address.
        /// </summary>
        public Dictionary<string, P2WpkhAddress> Addresses { get; set; }

        public byte[] PassphraseChallenge { get; set; }

        /// <summary>
        /// The WalletGuid correlates the X1WalletFile and the X1WalletMetadataFile.
        /// </summary>
        public Guid WalletGuid { get; set; }
        public string WalletName { get; set; }
        public string Comment { get; set; }
        public int Version { get; set; }
    }

    public class X1WalletMetadataFile
    {
        public const string FileExtension = ".x1wallet.metadata.json";

        public int MetadataVersion { get; set; }

        /// <summary>
        /// The WalletGuid correlates the X1WalletFile and the X1WalletMetadataFile.
        /// </summary>
        public Guid WalletGuid { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        public int SyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 SyncedHash { get; set; }

        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 CheckpointHash { get; set; }

        public int CheckpointHeight { get; set; }

        public Dictionary<int, BlockMetadata> Blocks { get; set; }
        
    }

    public class BlockMetadata
    {
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 HashBlock { get; set; }

        [JsonConverter(typeof(UInt256JsonConverter))]
        public Dictionary<uint256, TransactionMetadata> Transactions { get; set; }
       
    }

    public class TransactionMetadata
    {
        public TxType TxType { get; set; }

        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 HashTx { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, UtxoMetadata> Received { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, UtxoMetadata> Spent { get; set; }
    }

    public class UtxoMetadata
    {
        public string HashHexAddress { get; set; }

        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 HashTx { get; set; }

        public int Index { get; set; }
        public long Satoshis { get; set; }

        public string GetKey()
        {
            return $"{this.HashTx}-{this.Index}";
        }
    }

    public enum TxType
    {
        /// <summary>
        /// The value has not been set.
        /// </summary>
        NotSet = 0,

        /// <summary>
        /// Coinbase transaction.
        /// </summary>
        Coinbase = 10,

        /// <summary>
        /// Legacy Coinstake transaction with 3 outputs.
        /// </summary>
        CoinstakeLegacy = 20,

        /// <summary>
        /// Coinstake transaction with 3 outputs.
        /// </summary>
        Coinstake = 21,

       /// <summary>
       /// The transaction spent wallet outputs, no outputs were received. 
       /// </summary>
        Spend = 30,

       /// <summary>
       /// The transaction added outputs to the wallet, but its type was not Coinbase, Legacy Coinstake or coinstake.
       /// </summary>
        Receive = 31,

       /// <summary>
       /// The transaction added outputs to the wallet, but they were not Coinbase, Legacy Coinstake or coinstake.
       /// In addition to that, the wallet received new unspent outputs. This normally means sending funds to oneself.
       /// </summary>
        SpendReceive = 32,

       /// <summary>
       /// Legacy ColdCoinstake.
       /// </summary>
       ColdCoinstakeLegacy = 40,

       /// <summary>
       /// ColdCoinstake.
       /// </summary>
        ColdCoinstake = 41
    }


}
