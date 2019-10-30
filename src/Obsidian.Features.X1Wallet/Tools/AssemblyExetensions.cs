using System.Reflection;

namespace Obsidian.Features.X1Wallet.Tools
{
    public static class AssemblyExtensions
    {
        /// <summary>
        /// // Pattern is: 1.0.*. The wildcard is: DateTime.Today.Subtract(new DateTime(2000, 1, 1)).Days;
        /// </summary>
        public static string GetShortVersionString(this Assembly assembly)
        {
            var version = assembly.GetName().Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }
}
