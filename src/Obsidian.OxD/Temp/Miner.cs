using System;
using System.Linq;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;
using Obsidian.Features.X1Wallet;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;

namespace Obsidian.OxD.Temp
{
    public static class Miner
    {
        /// <summary>
        /// https://github.com/MicroBitcoinOrg/MicroBitcoin/blob/master/src/miner.cpp
        /// https://github.com/bitcoin/bips/blob/master/bip-0141.mediawiki
        /// https://bitcoin.stackexchange.com/questions/74235/how-to-generate-a-p2wsh-address
        /// https://bitcoin.stackexchange.com/questions/59231/how-to-sign-a-segwit-transaction-via-nbitcoin
        /// https://github.com/libbitcoin/libbitcoin-system/wiki/P2WPKH-Transactions
        ///  </summary>
        /// <param Command="fullNode"></param>
        /// <param Command="targetAddress"></param>
        public static void Start(FullNode fullNode)
        {
            Task.Run(async () =>
            {
                try
                {
                    // The X1Wallet feature does not automatically start the wallet, so load one before mining.
                    // You can create a wallet via the wallet ui or via walletController.CreateKeyWalletAsync()
                    var walletName = "blacky";
                    
                    var walletController = fullNode.NodeService<WalletController>();
                    walletController.SetWalletName(walletName);
                    await walletController.LoadAsync();


                    var addressesModel = await walletController.GetUnusedReceiveAddresses();
                    var bech32 = addressesModel.Addresses.First(a => a.IsChange == false).Address;
                    var mineToAddress = new BitcoinWitPubKeyAddress(bech32, fullNode.Network);

                    var powMining = fullNode.NodeService<IPowMining>();
                    var reserveScript = new ReserveScript { ReserveFullNodeScript = mineToAddress.ScriptPubKey };
                    Console.WriteLine($"Mining to address {bech32} in wallet {walletName} (ScriptPubKey: {mineToAddress.ScriptPubKey})");
                    var blockHashes = powMining.GenerateBlocks(reserveScript, (ulong)50000, uint.MaxValue);
                }
                catch (Exception e)

                {
                    Console.WriteLine($"Cannot Mine: {e.Message}");
                }

            });
        }
    }
}
