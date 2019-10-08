using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NBitcoin;
using NBitcoin.BuilderExtensions;
using Obsidian.Features.X1Wallet.Adapters;
using Obsidian.Features.X1Wallet.Models;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Obsidian.Features.X1Wallet
{
    public class IWalletManagerStakingAdapter : IWalletManager
    {
        WalletManagerWrapper walletManagerWrapper;
        string walletName;
        //public IWalletManagerStakingAdapter(WalletManagerWrapper walletManagerWrapper)
        //{
        //    this.walletManagerWrapper = walletManagerWrapper;
        //}

        public void SetWalletManagerWrapper(WalletManagerWrapper walletManagerWrapper, string walletName)
        {
            this.walletManagerWrapper = walletManagerWrapper;
            this.walletName = walletName;
        }

        public Dictionary<string, ScriptTemplate> GetValidStakingTemplates()
        {
            return new Dictionary<string, ScriptTemplate> {
                { "P2PK", PayToPubkeyTemplate.Instance },
                { "P2PKH", PayToPubkeyHashTemplate.Instance },
                { "P2WPKH", PayToWitPubKeyHashTemplate.Instance }
            };
        }

        public uint256 WalletTipHash
        {
            get
            {
                using (var context = this.walletManagerWrapper.GetWalletContext(this.walletName))
                {
                    return new uint256(context.WalletManager.WalletLastBlockSyncedHash);
                }
            }
            set { throw new NotImplementedException(); }
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWalletForStaking(string walletName, int confirmations = 1)
        {
            Debug.Assert(walletName == this.walletName);

            var utxos = new List<UnspentKeyAddressOutput>();
            using (var context = this.walletManagerWrapper.GetWalletContext(this.walletName))
            {
                utxos.AddRange(context.WalletManager.GetAllSpendableTransactions(confirmations));
            }

            var utxoReferences = new List<UnspentOutputReference>();
            foreach (var utxo in utxos)
            {
                utxoReferences.Add(new UnspentOutputReference {Confirmations = utxo.Confirmations, Transaction = utxo.Transaction, Address = utxo.Address.ToFakeHdAddress()});
            }

            return utxoReferences;
        }

        public Wallet GetWalletByName(string walletName)
        {
            Debug.Assert(walletName == this.walletName);
            var wallet = new WalletStakingAdapter(this.walletManagerWrapper, this.walletName);
            return wallet;
        }

        public IEnumerable<BuilderExtension> GetTransactionBuilderExtensionsForStaking()
        {
            return new List<BuilderExtension>();
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public int WalletTipHeight
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        public IEnumerable<UnspentOutputReference> GetSpendableTransactionsInWallet(string walletName, int confirmations = 0)
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

        public Wallet LoadWallet(string password, string name)
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

        public void ProcessBlock(Block block, ChainedHeader chainedHeader)
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



        public ICollection<uint256> GetFirstWalletBlockLocator()
        {
            throw new NotImplementedException();
        }

        public (string folderPath, IEnumerable<string>) GetWalletsFiles()
        {
            throw new NotImplementedException();
        }

        public bool ContainsWallets { get; }
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
    }
}
