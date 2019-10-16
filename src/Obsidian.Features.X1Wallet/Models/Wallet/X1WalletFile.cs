using System;
using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
    public class X1WalletFile
    {
        public const string FileExtension = ".x1wallet.json";

        public int Version { get; set; }

        public string WalletName { get; set; }

        public string Comment { get; set; }

        /// <summary>
        /// The BIP-0044 Coin Type or 0 if not defined.
        /// </summary>
        public int CoinType { get; set; }

        /// <summary>
        /// A string to indentify the network, e.g. ODX, tODX, BTC, tBTC.
        /// </summary>
        public string CoinTicker { get; set; }

        /// <summary>
        /// The WalletGuid correlates the X1WalletFile and the X1WalletMetadataFile.
        /// </summary>
        public Guid WalletGuid { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime ModifiedUtc { get; set; }

        public DateTime LastBackupUtc { get; set; }

        public int SyncFromHeight { get; set; }

        public byte[] PassphraseChallenge { get; set; }

        /// <summary>
        /// The key is the HashHex of an address. The value contains the transaction data for that address.
        /// </summary>
        public Dictionary<string, P2WpkhAddress> Addresses { get; set; }


    }
}
