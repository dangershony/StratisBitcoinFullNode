namespace Obsidian.Features.X1Wallet.Models
{
    public sealed class NodeInfo
    {
        public int ProcessId;
        public string ProcessName;
        public string MachineName;
        public string Program;
        public long StartupTime;
        public string NetworkName;
        public string CoinTicker;
        public string DataDirectoryPath;
        public bool Testnet;
        public string[] Features;
        public string Agent;
        public long MinTxFee;
        public long MinTxRelayFee;
    }
}
