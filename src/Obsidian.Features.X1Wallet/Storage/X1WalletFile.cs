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
        public Dictionary<string, P2WpkhAddress> P2WPKHAddresses { get; set; }

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
        public string SyncedHash { get; set; }

        public string CheckpointHash { get; set; }

        public int CheckpointHeight { get; set; }

        public Dictionary<int, BlockMetadata> Blocks { get; set; }
        
    }

    public class BlockMetadata
    {
        public string HashBlock { get; set; }

        public Dictionary<string, TransactionMetadata> ConfirmedTransactions { get; set; }
    }

    public class TransactionMetadata
    {
        public bool IsCoinbase;
        public bool IsCoinstake;
        public string HashTx { get; set; }
        public List<UtxoMetadata> ReceivedUtxos { get; set; } 
    }

    public class UtxoMetadata
    {
        public string HashHex;
        public string HashTx { get; set; }
        public int Index { get; set; }
        public long Satoshis { get; set; }
        public bool IsSpent { get; set; }
    }

    public enum TxType
    {
        Coinbase,
        Coinstake,
        Spend,
        Receive,
        SpendReceive
    }


}
