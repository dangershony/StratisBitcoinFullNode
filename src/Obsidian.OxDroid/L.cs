using System.Runtime.CompilerServices;
using Android.Util;

namespace Obsidian.DroidD
{
    public static class L
    {
        public static void Info(this object o, string message,[CallerMemberName] string caller="")
        {
            Log.Info("Obsidian.DroidD", $"|>>> In {o.GetType().Name}.{caller}(): {message}");
        }
    }
}