using NBitcoin;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Staking;

namespace Obsidian.Features.X1Wallet.Models.Api.Responses
{
    public class WalletInformation
    {
        public string WalletName;
        public string Coin;
        public string WalletFilePath;
        public int SyncedHeight;
        public uint256 SyncedHash;
        public Balance Balance;
        public MemoryPoolMetadata MemoryPool;
        public int Adresses;
        public string DefaultAddress;
        public string UnusedAddress;
        public StakingInfo StakingInfo;
        public string AssemblyVersion;
       
    }
}
