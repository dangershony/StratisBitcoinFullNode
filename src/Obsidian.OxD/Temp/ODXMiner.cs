using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;

namespace Obsidian.OxD.Temp
{
    public static class ODXMiner
    {
        static BitcoinSecret _minerSecret;
        static HdAddress _hdDddress;
        public static async void Run(Stratis.Bitcoin.IFullNode node)
        {
           
            //await Task.Delay(15000);
            //var fullNode = (FullNode)node;


            //CreateWallet(fullNode);
            //CreateWalletFromMnemonic(fullNode);
            //Task.Run(() =>
            //{
            //    SetMinerSecret(fullNode);
            //    var script = new ReserveScript { ReserveFullNodeScript = _minerSecret.ScriptPubKey.WitHash.GetAddress(fullNode.Network).ScriptPubKey };
            //    var blockHashes = fullNode.NodeService<IPowMining>().GenerateBlocks(script, (ulong)100, uint.MaxValue);
            //});



            //Spend(fullNode);


            ;
        }

       

        async static void Spend(FullNode fullNode)
        {
            BitcoinAddress destinationAddress = new Key().PubKey.WitHash.GetAddress(fullNode.Network);  // random dest address
            var txHash = new uint256("dd175f7979899999d55c0ab9a623bf81cbeda00305a0ec7864ea9aa79c89aa2e");

            var tx = fullNode.Network.CreateTransaction(
                "010000002f7bf15c010000000000000000000000000000000000000000000000000000000000000000ffffffff025900ffffffff010084d717000000002200208c3ea422668a241d34abdb9e826e66b4caea737e9fcd45ab0226125d312662f100000000");

            var coin = new Coin(tx, 0);

            string walletName = "blackstone";
            string accountName = "account 0";
            string walletPassword = "password";
           
            Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
            var adresses = wallet.GetAllAddresses().ToArray();

            HdAddress sourceHdAdr = null;
            foreach (var a in adresses)
            {
                sourceHdAdr = a;
                break;
            }
            Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, adresses[0]).PrivateKey;

            

            TransactionBuilder builder = new TransactionBuilder(fullNode.Network);
            builder.AddCoins(new[] {coin});
            builder.AddKeys(new[] {extendedPrivateKey});
            builder.Send(destinationAddress, Money.Coins(1));
            builder.SendFees(Money.Coins(0.001m));
            builder.SetChange(sourceHdAdr.Pubkey.WitHash.GetAddress(fullNode.Network));
            var signedTx = builder.BuildTransaction(true);
            bool success = builder.Verify(signedTx);
            fullNode.BroadcasterManager().BroadcastTransactionAsync(signedTx).GetAwaiter().GetResult();
            uint256 hash = signedTx.GetHash();
            var hashString = hash.ToString();
        }
       

        /*
        public static void SetMinerSecret(CoreNode coreNode, string walletName = "mywallet", string walletPassword = "password", string accountName = "account 0", string miningAddress = null)
        {
            if (coreNode.MinerSecret == null)
            {
                HdAddress address;
                if (!string.IsNullOrEmpty(miningAddress))
                {
                    address = coreNode.FullNode.WalletManager().GetAccounts(walletName).Single(a => a.Name == accountName).GetCombinedAddresses().Single(add => add.Address == miningAddress);
                }
                else
                {
                    address = coreNode.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));
                }

                coreNode.MinerHDAddress = address;

                Wallet wallet = coreNode.FullNode.WalletManager().GetWalletByName(walletName);
                Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address).PrivateKey;
                coreNode.SetMinerSecret(new BitcoinSecret(extendedPrivateKey, coreNode.FullNode.Network));
            }
        }
        */
     
        public static void SetMinerSecret(FullNode fullNode)
        {

           
            string walletName = "blackstone";
            string walletPassword = "fhdsjkfhjksdlhfjkdlshfkdshfk";
            string accountName = "account 0";

            string miningAddress = null;

            if (_minerSecret == null)
            {
                HdAddress address;
                if (!string.IsNullOrEmpty(miningAddress))
                {
                    address = fullNode.WalletManager().GetAccounts(walletName).Single(a => a.Name == accountName).GetCombinedAddresses().Single(add => add.Address == miningAddress);
                }
                else
                {
                    address = fullNode.WalletManager().GetUnusedAddress(new WalletAccountReference(walletName, accountName));

                }

                _hdDddress = address;

                Wallet wallet = fullNode.WalletManager().GetWalletByName(walletName);
                Key extendedPrivateKey = wallet.GetExtendedPrivateKeyForAddress(walletPassword, address).PrivateKey;
                _minerSecret = new BitcoinSecret(extendedPrivateKey, fullNode.Network);
            }
        }
        /*
        public static (HdAddress AddressUsed, List<uint256> BlockHashes) MineBlocks(CoreNode node, int numberOfBlocks, bool syncNode = true, string walletName = "mywallet", string walletPassword = "password", string accountName = "account 0", string miningAddress = null)
        {
            Guard.NotNull(node, nameof(node));

            if (numberOfBlocks == 0)
                throw new ArgumentOutOfRangeException(nameof(numberOfBlocks), "Number of blocks must be greater than zero.");

            SetMinerSecret(node, walletName, walletPassword, accountName, miningAddress);

            var script = new ReserveScript { ReserveFullNodeScript = node.MinerSecret.ScriptPubKey };
            var blockHashes = node.FullNode.Services.ServiceProvider.GetService<IPowMining>().GenerateBlocks(script, (ulong)numberOfBlocks, uint.MaxValue);

            if (syncNode)
                TestBase.WaitLoop(() => IsNodeSynced(node));

            return (node.MinerHDAddress, blockHashes);
        }
        */
        public static WalletManager WalletManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IWalletManager>() as WalletManager;
        }

        public static IBroadcasterManager BroadcasterManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IBroadcasterManager>();
        }
    }

   
}
