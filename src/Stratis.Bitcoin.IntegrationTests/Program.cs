using System;

namespace Stratis.Bitcoin.IntegrationTests
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            try
            {

            
            Console.WriteLine("========================================================");
            Console.WriteLine("STARting WalletCanCatchupWithBestChain");
            Console.WriteLine("========================================================");
            new WalletTests().WalletCanCatchupWithBestChain();

            Console.WriteLine("========================================================");
            Console.WriteLine("STARting WalletCanReorg");
            Console.WriteLine("========================================================");
            new WalletTests().WalletCanReorg();

            Console.WriteLine("========================================================");
            Console.WriteLine("STARting Given__TheNodeHadAReorg_And_ConensusTipIsdifferentFromWalletTip__When__ANewBlockArrives__Then__WalletCanRecover");
            Console.WriteLine("========================================================");
            new WalletTests().Given__TheNodeHadAReorg_And_ConensusTipIsdifferentFromWalletTip__When__ANewBlockArrives__Then__WalletCanRecover();

            Console.WriteLine("========================================================");
            Console.WriteLine("STARting Given__TheNodeHadAReorg_And_WalletTipIsBehindConsensusTip__When__ANewBlockArrives__Then__WalletCanRecover");
            Console.WriteLine("========================================================");
            new WalletTests().Given__TheNodeHadAReorg_And_WalletTipIsBehindConsensusTip__When__ANewBlockArrives__Then__WalletCanRecover();

            Console.WriteLine("========================================================");
            Console.WriteLine("STARting WalletCanRecoverOnStartup");
            Console.WriteLine("========================================================");
            new WalletTests().WalletCanRecoverOnStartup();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
