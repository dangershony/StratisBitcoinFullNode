using System;
using System.Collections.Generic;
using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
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
        public uint256 SyncedHash { get; set; }

        public uint256 CheckpointHash { get; set; }

        public int CheckpointHeight { get; set; }

        public Dictionary<int, BlockMetadata> Blocks { get; set; }
        
    }
}