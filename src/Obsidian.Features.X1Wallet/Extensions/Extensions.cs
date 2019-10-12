using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Extensions
{
    public static class Tools
    {
        public static void NotNull<K, T>(ref Dictionary<K, T> list, int capacity)
        {
            if (list == null)
                list = new Dictionary<K, T>(capacity);
        }
    }
}
