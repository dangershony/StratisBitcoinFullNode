namespace Obsidian.Features.X1Wallet.Models
{
    public sealed class DaemonInfo
    {
        public int ProcessId;
        public string ProcessName;
        public string MachineName;
        public long ProcessMemory;
        public string CodeBase;
        public long StartupTime;
        public string NetworkName;
        public string CoinTicker;
        public bool Testnet;
        public string AssemblyVersion;
        public string AgentName;
        public long MinTxFee;
        public long MinTxRelayFee;
        public StringItem[] Features;
        public string WalletPath;
        public StringItem[] WalletFiles;
    }

    public class StringItem
    {
        public string NamedItem;
    }
}
