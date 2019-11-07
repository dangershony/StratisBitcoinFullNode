using System;
using System.Collections.Generic;
using System.IO;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Stratis.Bitcoin.Configuration;

namespace Obsidian.Features.X1Wallet.Tools
{
    public static class WalletHelper
    {
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
            var serializedWallet = Serializer.Serialize(x1WalletFile);
            File.WriteAllText(filePath, serializedWallet);
        }

        public static X1WalletFile LoadX1WalletFile(string filePath)
        {
            var file = File.ReadAllText(filePath);
            var x1WalletFile = Serializer.Deserialize<X1WalletFile>(file);
            if (Path.GetFileName(filePath.Replace(X1WalletFile.FileExtension, string.Empty).Replace($".{x1WalletFile.CoinTicker}", string.Empty)) != x1WalletFile.WalletName)
                throw new InvalidOperationException($"The wallet name {x1WalletFile.WalletName} inside of file {filePath} doesn't match the naming convention for {X1WalletFile.FileExtension}-files. Please correct this.");

            // a good place for file format updates is here

            return x1WalletFile;
        }

        public static X1WalletMetadataFile CreateX1WalletMetadataFile(this X1WalletFile x1WalletFile, uint256 genesisHash)
        {
            return new X1WalletMetadataFile
            {
                X1WalletAssemblyVersion = GetX1WalletAssemblyVersion(),
                WalletGuid = x1WalletFile.WalletGuid,
                CheckpointHash = genesisHash,
                SyncedHash = genesisHash,
                Blocks = new Dictionary<int, BlockMetadata>(),
                MemoryPool = new MemoryPoolMetadata { Entries = new HashSet<MemoryPoolEntry>()}
            };
        }

        public static X1WalletMetadataFile LoadOrCreateX1WalletMetadataFile(string x1WalletMetadataFilePath, X1WalletFile x1WalletFile, uint256 genesisHash)
        {
            X1WalletMetadataFile x1WalletMetadataFile;
            if (File.Exists(x1WalletMetadataFilePath))
            {
                x1WalletMetadataFile = Serializer.Deserialize<X1WalletMetadataFile>(File.ReadAllText(x1WalletMetadataFilePath));
                if (x1WalletMetadataFile.X1WalletAssemblyVersion == GetX1WalletAssemblyVersion() &&
                    x1WalletMetadataFile.WalletGuid == x1WalletFile.WalletGuid)
                    return x1WalletMetadataFile;
                return CreateX1WalletMetadataFile(x1WalletFile, genesisHash);

            }

            x1WalletMetadataFile = x1WalletFile.CreateX1WalletMetadataFile(genesisHash);
            x1WalletMetadataFile.SaveX1WalletMetadataFile(x1WalletMetadataFilePath);
            return x1WalletMetadataFile;
        }

        public static void SaveX1WalletMetadataFile(this X1WalletMetadataFile x1WalletMetadataFile, string filePath)
        {
            var serializedWallet = Serializer.Serialize(x1WalletMetadataFile);
            File.WriteAllText(filePath, serializedWallet);
        }

        static string GetX1WalletAssemblyVersion()
        {
            return  typeof(X1WalletMetadataFile).Assembly.GetShortVersionString();
        }
    }
}
