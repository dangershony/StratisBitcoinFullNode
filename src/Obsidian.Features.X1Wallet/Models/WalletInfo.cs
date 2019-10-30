using System.Collections.Generic;
using NBitcoin;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Staking;

namespace Obsidian.Features.X1Wallet.Models
{
    public sealed class WalletInfo
    {
        public int ConsensusTipHeight;
        public uint256 ConsensusTipHash;
        public int ConsensusTipAge;
        public int BlockStoreHeight;
        public uint256 BlockStoreHash;
        public int MaxTipAge;
        public bool IsAtBestChainTip;
        public ConnectionInfo ConnectionInfo;
        public string AssemblyName;
        public string AssemblyVersion;
        public WalletDetails WalletDetails;
    }

    public sealed class ConnectionInfo
    {
        public int BestPeerHeight;
        public uint256 BestPeerHash;
        public int InBound;
        public int OutBound;
        public List<PeerInfo> Peers;
    }

    public sealed class PeerInfo
    {
        public string Version;
        public string RemoteSocketEndpoint;
        public int BestReceivedTipHeight;
        public uint256 BestReceivedTipHash;
        public bool IsInbound;
    }
    public class WalletDetails
    {
        public string WalletName;
        public string WalletFilePath;
        public int SyncedHeight;
        public uint256 SyncedHash;
        public Balance Balance;
        public MemoryPoolMetadata MemoryPool;
        public int Adresses;
        public string DefaultAddress;
        public string UnusedAddress;
        public StakingInfo StakingInfo;
        public string PassphraseChallenge;
    }
}
