using System;
using System.Threading.Tasks;
using NBitcoin;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Miner;
using Stratis.Bitcoin.Features.Miner.Interfaces;

namespace Obsidian.ObsidianD.Temp
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
        public static void Start(FullNode fullNode, Script targetAddress)
        {
            Task.Run(() =>
            {
                try
                {
                    var powMining = fullNode.NodeService<IPowMining>();
                    var reserveScript = new ReserveScript { ReserveFullNodeScript = targetAddress };
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
