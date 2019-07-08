﻿using System;
using System.Collections.Generic;
using NBitcoin;
using Newtonsoft.Json;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.Features.SegWitWallet
{
    public class KeyWallet
    {
        /// <summary>
        /// The type of this wallet implementation.
        /// </summary>
        [JsonProperty(PropertyName = "wallettype")]
        public string WalletType { get; set; }

        /// <summary>
        /// The implementation version.
        /// </summary>
        [JsonProperty(PropertyName = "wallettypeversion")]
        public int WalletTypeVersion { get; set; }

        /// <summary>
        /// The name of this wallet.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the creation time.
        /// </summary>
        [JsonProperty(PropertyName = "creationTime")]
        [JsonConverter(typeof(DateTimeOffsetConverter))]
        public DateTimeOffset CreationTime { get; set; }

        /// <summary>
        /// A collection of addresses contained in the wallet.
        /// </summary>
        [JsonProperty(PropertyName = "addresses")]
        public ICollection<KeyAddress> Addresses { get; set; }

        /// <summary>
        /// The height of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHeight")]
        public int LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(PropertyName = "lastBlockSyncedHash", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// Gets or sets the Merkle path.
        /// </summary>
        [JsonProperty(PropertyName = "blockLocator", ItemConverterType = typeof(UInt256JsonConverter))]
        public ICollection<uint256> BlockLocator { get; set; }


 
    }
}
