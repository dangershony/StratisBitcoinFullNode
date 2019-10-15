using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.Features.X1Wallet.Storage
{
    public static class WalletHelper
    {
        static readonly JsonSerializerSettings JsonSettings;

        static WalletHelper()
        {
            JsonSettings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            JsonSettings.Converters.Add(new UInt256JsonConverter());
            JsonSettings.NullValueHandling = NullValueHandling.Ignore;
        }


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
            var serializedWallet = JsonConvert.SerializeObject(x1WalletFile, Formatting.Indented, JsonSettings);
            File.WriteAllText(filePath, serializedWallet);
        }

        public static X1WalletFile LoadX1WalletFile(string filePath)
        {
            var file = File.ReadAllText(filePath);
            var x1WalletFile = JsonConvert.DeserializeObject<X1WalletFile>(file, JsonSettings);
            if (Path.GetFileName(filePath.Replace(X1WalletFile.FileExtension, string.Empty).Replace($".{x1WalletFile.CoinTicker}", string.Empty)) != x1WalletFile.WalletName)
                throw new InvalidOperationException($"The wallet name {x1WalletFile.WalletName} inside of file {filePath} doesn't match the naming convention for {X1WalletFile.FileExtension}-files. Please correct this.");

            // a good place for file format updates is here

            return x1WalletFile;
        }

        public static X1WalletMetadataFile CreateX1WalletMetadataFile(this X1WalletFile x1WalletFile, int metadataVersion, uint256 genesisHash)
        {
            return new X1WalletMetadataFile
            {
                MetadataVersion = metadataVersion,
                WalletGuid = x1WalletFile.WalletGuid,
                CheckpointHash = genesisHash,
                SyncedHash = genesisHash,
                Blocks = new Dictionary<int, BlockMetadata>()
            };
        }

        public static X1WalletMetadataFile LoadOrCreateX1WalletMetadataFile(string x1WalletMetadataFilePath, X1WalletFile x1WalletFile, int expectedMetadataVersion, uint256 genesisHash)
        {
            X1WalletMetadataFile x1WalletMetadataFile;
            if (File.Exists(x1WalletMetadataFilePath))
            {
                x1WalletMetadataFile = JsonConvert.DeserializeObject<X1WalletMetadataFile>(File.ReadAllText(x1WalletMetadataFilePath), JsonSettings);
                if (x1WalletMetadataFile.MetadataVersion != expectedMetadataVersion)
                    throw new Exception($"The program expects Metadata version {expectedMetadataVersion}, but the file {x1WalletMetadataFilePath} has Metadata version {x1WalletMetadataFile.MetadataVersion}. If you backup and delete the current Metadata file, a new file with the new version will be created.");
                if (x1WalletMetadataFile.WalletGuid != x1WalletFile.WalletGuid)
                    throw new InvalidOperationException($"The WalletGuid in the {X1WalletFile.FileExtension}-file and the {X1WalletMetadataFile.FileExtension}-file for wallet name {x1WalletFile.WalletName} do not match. Please fix this.");

            }
            else
            {
                x1WalletMetadataFile = x1WalletFile.CreateX1WalletMetadataFile(expectedMetadataVersion, genesisHash);
                x1WalletMetadataFile.SaveX1WalletMetadataFile(x1WalletMetadataFilePath);
            }
            return x1WalletMetadataFile;
        }

        public static void SaveX1WalletMetadataFile(this X1WalletMetadataFile x1WalletMetadataFile, string filePath)
        {
            var serializedWallet = JsonConvert.SerializeObject(x1WalletMetadataFile, Formatting.Indented, JsonSettings);
            File.WriteAllText(filePath, serializedWallet);
        }
    }
}
