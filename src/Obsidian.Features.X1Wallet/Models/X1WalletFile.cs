using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Utilities.JsonConverters;
using System.Linq;

namespace Obsidian.Features.X1Wallet.Models
{
    public class X1WalletFile
    {
        public const string FileExtension = ".x1wallet.json";

        /// <summary>
        /// The key is the HashHex of an address. The value contains the transaction data for that address.
        /// </summary>
        public Dictionary<string, P2WPKHAddress> P2WPKHAddresses { get; set; }

        public byte[] PassphraseChallenge { get; set; }

        /// <summary>
        /// The WalletGuid correlates the X1WalletFile and the X1WalletMetadataFile.
        /// </summary>
        public Guid WalletGuid { get; set; }
        public string WalletName { get; set; }
        public string Comment { get; set; }
        public int Version { get; set; }
    }

    public static class WalletHelper
    {
        static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
        public static string GetX1WalletFilepath(this string walletName, Network network, DataFolder dataFolder)
        {
            if (string.IsNullOrWhiteSpace(walletName))
                throw new ArgumentNullException(nameof(walletName));

            var fileName = $"{walletName}.{network.CoinTicker}{X1WalletFile.FileExtension}";
            string filePath = Path.Combine(dataFolder.WalletPath, fileName);
            return filePath;
        }

        public static string GetX1WalletMetaDataFilepath(this string walletName, Network network, DataFolder dataFolder)
        {
            if (string.IsNullOrWhiteSpace(walletName))
                throw new ArgumentNullException(nameof(walletName));

            var fileName = $"{walletName}.{network.CoinTicker}{X1WalletMetadataFile.FileExtension}";
            string filePath = Path.Combine(dataFolder.WalletPath, fileName);
            return filePath;
        }

        public static void SaveX1WalletFile(this X1WalletFile x1WalletFile, string filePath)
        {
            var serializedWallet = JsonConvert.SerializeObject(x1WalletFile, Formatting.Indented, jsonSettings);
            File.WriteAllText(filePath, serializedWallet);
        }

        public static X1WalletFile LoadX1WalletFile(string filePath)
        {
            var file = File.ReadAllText(filePath);
            var x1WalletFile = JsonConvert.DeserializeObject<X1WalletFile>(file, jsonSettings);
            if (Path.GetFileName(filePath.Replace(X1WalletFile.FileExtension, string.Empty).Replace($".{x1WalletFile.P2WPKHAddresses.Values.First().CoinTicker}", string.Empty)) != x1WalletFile.WalletName)
                throw new InvalidOperationException($"The wallet name {x1WalletFile.WalletName} inside of file {filePath} doesn't match the naming convention for {X1WalletFile.FileExtension}-files. Please correct this.");
            return x1WalletFile;
        }

        public static X1WalletMetadataFile CreateX1WalletMetadataFile(this X1WalletFile x1WalletFile)
        {
            return new X1WalletMetadataFile
            {
                BlockLocator = new HashSet<uint256>(),
                Transactions = new Dictionary<string, List<TransactionData>>(),
                UsedAddresses = new HashSet<string>(),
                WalletGuid = x1WalletFile.WalletGuid
            };
        }

        public static X1WalletMetadataFile LoadOrCreateX1WalletMetadataFile(string x1WalletMetadataFilePath, X1WalletFile x1WalletFile)
        {
            X1WalletMetadataFile x1WalletMetadataFile;
            if (File.Exists(x1WalletMetadataFilePath))
            {
                x1WalletMetadataFile = JsonConvert.DeserializeObject<X1WalletMetadataFile>(File.ReadAllText(x1WalletMetadataFilePath), jsonSettings);
                if (x1WalletMetadataFile.WalletGuid != x1WalletFile.WalletGuid)
                    throw new InvalidOperationException($"The WalletGuid in the {X1WalletFile.FileExtension}-file and the {X1WalletMetadataFile.FileExtension}-file for wallet name {x1WalletFile.WalletName} do not match. Please fix this.");

            }
            x1WalletMetadataFile = x1WalletFile.CreateX1WalletMetadataFile();
            x1WalletMetadataFile.SaveX1WalletMetadataFile(x1WalletMetadataFilePath);
            return x1WalletMetadataFile;
        }

        public static void SaveX1WalletMetadataFile(this X1WalletMetadataFile x1WalletMetadataFile, string filePath)
        {
            var serializedWallet = JsonConvert.SerializeObject(x1WalletMetadataFile, Formatting.Indented, jsonSettings);
            File.WriteAllText(filePath, serializedWallet);
        }




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
        public int LastBlockSyncedHeight { get; set; }

        /// <summary>
        /// The hash of the last block that was synced.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(UInt256JsonConverter))]
        public uint256 LastBlockSyncedHash { get; set; }

        /// <summary>
        /// Contains the HashHex of all used addresses.
        /// </summary>
        public HashSet<string> UsedAddresses { get; set; }

        /// <summary>
        /// Gets or sets the Merkle path.
        /// </summary>
        [JsonProperty(ItemConverterType = typeof(UInt256JsonConverter))]
        public HashSet<uint256> BlockLocator { get; set; }

        /// <summary>
        /// The key is the HashHex of an address. The value contains the transaction data for that address.
        /// </summary>
        public Dictionary<string, List<TransactionData>> Transactions { get; set; }


    }
}
