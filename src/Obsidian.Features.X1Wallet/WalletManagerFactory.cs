using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Api.Responses;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Tools;
using Stratis.Bitcoin.Base;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Mining;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    public class WalletManagerFactory : IDisposable
    {
        readonly object lockObject = new object();
        readonly DataFolder dataFolder;
        readonly ChainIndexer chainIndexer;
        readonly Network network;
        readonly IBroadcasterManager broadcasterManager;
        readonly ILoggerFactory loggerFactory;
        readonly INodeLifetime nodeLifetime;
        readonly IInitialBlockDownloadState initialBlockDownloadState;
        readonly ISignals signals;
        readonly IBlockStore blockStore;
        readonly ITimeSyncBehaviorState timeSyncBehaviorState;
        readonly IBlockProvider blockProvider;
        readonly IConsensusManager consensusManager;
        readonly IStakeChain stakeChain;

        static readonly RNGCryptoServiceProvider Rng = new RNGCryptoServiceProvider();

        WalletManager walletManager;

        public WalletManagerFactory(DataFolder dataFolder, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory,
            INodeLifetime nodeLifetime, ISignals signals, IBlockStore blockStore, ITimeSyncBehaviorState timeSyncBehaviorState, IInitialBlockDownloadState initialBlockDownloadState, IBlockProvider blockProvider, IConsensusManager consensusManager, IStakeChain stakeChain)
        {
            this.dataFolder = dataFolder;
            this.chainIndexer = chainIndexer;
            this.network = network;
            this.broadcasterManager = broadcasterManager;
            this.loggerFactory = loggerFactory;
            this.nodeLifetime = nodeLifetime;
            this.initialBlockDownloadState = initialBlockDownloadState;
            this.signals = signals;
            this.blockStore = blockStore;
            this.timeSyncBehaviorState = timeSyncBehaviorState;
            this.blockProvider = blockProvider;
            this.consensusManager = consensusManager;
            this.stakeChain = stakeChain;
        }

        public WalletContext GetWalletContext(string walletName, bool doNotCheck = false)
        {
            if (doNotCheck)
            {
                if (this.walletManager == null)
                    return null;
                return new WalletContext(this.walletManager);
            }

            if (walletName == null)
                throw new ArgumentNullException(nameof(walletName));

            if (this.walletManager != null)
            {
                if (this.walletManager.WalletName == walletName)
                    return new WalletContext(this.walletManager);
                throw new InvalidOperationException($"Invalid request for wallet {walletName} - the current wallet is {this.walletManager.WalletName}");
            }
            lock (this.lockObject)
            {
                if (this.walletManager == null)
                {
                    LoadWalletAndCreateWalletManagerInstance(walletName);
                    Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
                }
            }
            return new WalletContext(this.walletManager);

        }

        WalletContext GetWalletContextPrivate()
        {
            return GetWalletContext(null, true);
        }

        void LoadWalletAndCreateWalletManagerInstance(string walletName)
        {
            string x1WalletFilePath = walletName.GetX1WalletFilepath(this.network, this.dataFolder);

            if (!File.Exists(x1WalletFilePath))
                throw new FileNotFoundException($"No wallet file found at {x1WalletFilePath}");


            if (this.walletManager != null)
            {
                if (this.walletManager.CurrentX1WalletFilePath != x1WalletFilePath)
                    throw new NotSupportedException(
                        "Core wallet manager already created, changing the wallet file while node and wallet are running is not currently supported.");
            }
            this.walletManager = new WalletManager(x1WalletFilePath, this.chainIndexer, this.network, this.dataFolder, this.broadcasterManager, this.loggerFactory, 
                 this.nodeLifetime,  this.timeSyncBehaviorState, this.signals, this.initialBlockDownloadState, this.blockStore, this.blockProvider, this.consensusManager, this.stakeChain);
        }

        public void CreateWallet(WalletCreateRequest walletCreateRequest)
        {
            string walletName = walletCreateRequest.Name;
            string filePath = walletName.GetX1WalletFilepath(this.network, this.dataFolder);

            if (File.Exists(filePath))
                throw new InvalidOperationException($"A wallet with the name {walletName} already exists at {filePath}!");

            if (string.IsNullOrWhiteSpace(walletCreateRequest.Password))
                throw new InvalidOperationException("A passphrase is required.");

            AddressHelper.Init(this.network);

            var now = DateTime.UtcNow;

            // Create the passphrase challenge
            var challengePlaintext = new byte[32];
            Rng.GetBytes(challengePlaintext);

            var x1WalletFile = new X1WalletFile
            {
                Addresses = new Dictionary<string, P2WpkhAddress>(),
                WalletGuid = Guid.NewGuid(),
                WalletName = walletName,
                CoinTicker = this.network.CoinTicker,
                CoinType = this.network.Consensus.CoinType,
                CreatedUtc = now,
                ModifiedUtc = now,
                SyncFromHeight = 0, // TODO
                Comment = "Your notes here!",
                Version = 1,
                PassphraseChallenge = VCL.EncryptWithPassphrase(walletCreateRequest.Password, challengePlaintext)
            };

            const int addressPoolSize = 1000;

            for (var i = 0; i < addressPoolSize; i++)
            {
                var bytes = new byte[32];
                Rng.GetBytes(bytes);
                var address = AddressHelper.CreateWithPrivateKey(bytes, walletCreateRequest.Password, VCL.EncryptWithPassphrase);
                x1WalletFile.Addresses.Add(address.Address, address);
            }
            if (x1WalletFile.Addresses.Count != addressPoolSize)
                throw new Exception("Something is seriously wrong, collision of random numbers detected. Do not use this wallet.");

            x1WalletFile.SaveX1WalletFile(filePath);

            X1WalletMetadataFile x1WalletMetadataFile = x1WalletFile.CreateX1WalletMetadataFile(this.network.GenesisHash);
            var x1WalletMetadataFilename = walletName.GetX1WalletMetaDataFilepath(this.network, this.dataFolder);
            x1WalletMetadataFile.SaveX1WalletMetadataFile(x1WalletMetadataFilename);
        }

        public WalletFilesResponse GetWalletsFiles()
        {
            return new WalletFilesResponse
            {
                WalletsPath = this.dataFolder.WalletPath,
                WalletFiles = Directory.EnumerateFiles(this.dataFolder.WalletPath, $"*{X1WalletFile.FileExtension}", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName).ToList()
            };
        }


        public void Repair(RepairRequest date)
        {
            int blockSyncStart = this.chainIndexer.GetHeightAtTime(DateTime.MinValue);
            WalletSyncManagerSyncFromHeightAsync(blockSyncStart);
        }

        void WalletSyncManagerSyncFromHeightAsync(int height)
        {
            ChainedHeader chainedHeader = this.chainIndexer.GetHeader(height);
            if (chainedHeader == null)
                throw new WalletException("Invalid block height");

            using var context = GetWalletContextPrivate();
            context.WalletManager.RemoveBlocks(chainedHeader);
        }

        public void Dispose()
        {
            using var context = GetWalletContextPrivate();
            context?.WalletManager?.Dispose();

        }

        
    }
}
