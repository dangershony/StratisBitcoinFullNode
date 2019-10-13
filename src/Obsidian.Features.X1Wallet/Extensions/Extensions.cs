using System.Collections.Generic;
using NBitcoin;

namespace Obsidian.Features.X1Wallet.Extensions
{
    public static class Tools
    {
        public static void NotNull<K, T>(ref Dictionary<K, T> list, int capacity)
        {
            if (list == null)
                list = new Dictionary<K, T>(capacity);
        }

        public static List<OutPoint> GetPrevOuts(Transaction transaction)
        {
            var prevOuts = new List<OutPoint>(transaction.Inputs.Count);
            foreach (TxIn input in transaction.Inputs)
            {
                prevOuts.Add(input.PrevOut);
            }

            return prevOuts;
        }
    }
}
