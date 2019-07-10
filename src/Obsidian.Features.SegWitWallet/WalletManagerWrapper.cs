using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Stratis.Bitcoin.AsyncWork;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.BlockStore;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;
using Stratis.Bitcoin.Signals;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Features.X1Wallet
{
    public partial class WalletManagerWrapper : IWalletManager
    {
        readonly DataFolder dataFolder;
        readonly ChainIndexer chainIndexer;
        readonly Network network;
        readonly IBroadcasterManager broadcasterManager;
        readonly ILoggerFactory loggerFactory;
        readonly ILogger logger;
        readonly IScriptAddressReader scriptAddressReader;
        readonly IDateTimeProvider dateTimeProvider;
        readonly INodeLifetime nodeLifetime;
        readonly IAsyncProvider asyncProvider;

        public WalletManagerWrapper(DataFolder dataFolder, ChainIndexer chainIndexer, Network network, IBroadcasterManager broadcasterManager, ILoggerFactory loggerFactory,
            IScriptAddressReader scriptAddressReader, IDateTimeProvider dateTimeProvider, INodeLifetime nodeLifetime, IAsyncProvider asyncProvider, ISignals signals, IBlockStore blockStore, StoreSettings storeSettings)
        {
            this.dataFolder = dataFolder;
            this.chainIndexer = chainIndexer;
            this.network = network;
            this.broadcasterManager = broadcasterManager;
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger(typeof(WalletManagerWrapper).FullName);
            this.scriptAddressReader = scriptAddressReader;
            this.dateTimeProvider = dateTimeProvider;
            this.nodeLifetime = nodeLifetime;
            this.asyncProvider = asyncProvider;

            ConstructWalletSyncManager(signals, blockStore, storeSettings);
        }



        WalletManager walletManager;

        public WalletManager GetManager(string walletName, bool doNotCheck = false)
        {
            if (doNotCheck)
                return this.walletManager;

            if (this.walletManager != null)
            {
                if (this.walletManager.Wallet.Name == walletName)
                    return this.walletManager;
                throw new ArgumentException("Invalid", nameof(walletName));
            }

            LoadWallet(null, walletName);
            Debug.Assert(this.walletManager != null, "The WalletSyncManager cannot be correctly initialized when the WalletManager is null");
            ((IWalletSyncManager)this).Start();
            return GetManager(walletName);
        }

        public Wallet LoadWallet(string password, string name)
        {
            var fileName = $"{name}{WalletManager.WalletFileExtension}";
            string filePath = Path.Combine(this.dataFolder.WalletPath, fileName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"No wallet file found at {filePath}");
            if (this.walletManager != null)
            {
                if (this.walletManager.CurrentWalletFilePath != filePath)
                    throw new NotSupportedException(
                        "Core wallet manager already created, changing the wallet file while node and wallet are running is not currently supported.");
            }
            this.walletManager = new WalletManager(filePath, this.chainIndexer, this.network, this.broadcasterManager, this.loggerFactory, this.scriptAddressReader, this.dateTimeProvider, this.nodeLifetime, this.asyncProvider);
            return null;
        }

        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            var filePathes = Directory.EnumerateFiles(this.dataFolder.WalletPath, $"*{WalletManager.WalletFileExtension}", SearchOption.TopDirectoryOnly);
            var files = filePathes.Select(Path.GetFileName);
            return (this.dataFolder.WalletPath, files);
        }

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate>();
        }

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
        {
            this.walletManager.ProcessBlock(block, chainedHeader);
        }

        #region  throw new NotImplementedException();

        public bool ContainsWallets => throw new NotImplementedException();

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public uint256 WalletTipHash
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public int WalletTipHeight
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInAccount(WalletAccountReference walletAccountReference, int confirmations = 0)
        {
            throw new NotImplementedException();
        }

        public Mnemonic CreateWallet(string password, string name, string passphrase = null, Mnemonic mnemonic = null)
        {
            throw new NotImplementedException();
        }

        public string SignMessage(string password, string walletName, string externalAddress, string message)
        {
            throw new NotImplementedException();
        }

        public bool VerifySignedMessage(string externalAddress, string message, string signature)
        {
            throw new NotImplementedException();
        }

        public void UnlockWallet(string password, string name, int timeout)
        {
            throw new NotImplementedException();
        }

        public void LockWallet(string name)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string password, string name, string mnemonic, DateTime creationTime, string passphrase = null)
        {
            throw new NotImplementedException();
        }

        public Wallet RecoverWallet(string name, ExtPubKey extPubKey, int accountIndex, DateTime creationTime)
        {
            throw new NotImplementedException();
        }

        public void DeleteWallet()
        {
            throw new NotImplementedException();
        }

        public HdAccount GetUnusedAccount(string walletName, string password)
        {
            throw new NotImplementedException();
        }

        public HdAccount GetUnusedAccount(Wallet wallet, string password)
        {
            throw new NotImplementedException();
        }

        public HdAddress GetUnusedAddress(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public HdAddress GetUnusedChangeAddress(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAddress> GetUnusedAddresses(WalletAccountReference accountReference, int count, bool isChange = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AccountHistory> GetHistory(string walletName, string accountName = null)
        {
            throw new NotImplementedException();
        }

        public AccountHistory GetHistory(HdAccount account)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AccountBalance> GetBalances(string walletName, string accountName = null)
        {
            throw new NotImplementedException();
        }

        public AddressBalance GetAddressBalance(string address)
        {
            throw new NotImplementedException();
        }

        public Wallet GetWallet(string walletName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<HdAccount> GetAccounts(string walletName)
        {
            throw new NotImplementedException();
        }

        public int LastBlockHeight()
        {
            throw new NotImplementedException();
        }

        public void RemoveBlocks(ChainedHeader fork)
        {
            throw new NotImplementedException();
        }

       

        public bool ProcessTransaction(Transaction transaction, int? blockHeight = null, Block block = null, bool isPropagated = true)
        {
            throw new NotImplementedException();
        }

        public void SaveWallet(Wallet wallet)
        {
            throw new NotImplementedException();
        }

        public void SaveWallets()
        {
            throw new NotImplementedException();
        }

        public string GetWalletFileExtension()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetWalletsNames()
        {
            throw new NotImplementedException();
        }

        public void UpdateLastBlockSyncedHeight(Wallet wallet, ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public void UpdateLastBlockSyncedHeight(ChainedHeader chainedHeader)
        {
            throw new NotImplementedException();
        }

        public Wallet GetWalletByName(string walletName)
        {
            throw new NotImplementedException();
        }

        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            throw new NotImplementedException();
        }

        public string GetExtPubKey(WalletAccountReference accountReference)
        {
            throw new NotImplementedException();
        }

        public ExtKey GetExtKey(WalletAccountReference accountReference, string password = "", bool cache = false)
        {
            throw new NotImplementedException();
        }

        public int? GetEarliestWalletHeight()
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset GetOldestWalletCreationTime()
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsByIds(string walletName, IEnumerable<uint256> transactionsIds)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveAllTransactions(string walletName)
        {
            throw new NotImplementedException();
        }

        public HashSet<(uint256, DateTimeOffset)> RemoveTransactionsFromDate(string walletName, DateTimeOffset fromDate)
        {
            throw new NotImplementedException();
        }

        #endregion

    }
}
