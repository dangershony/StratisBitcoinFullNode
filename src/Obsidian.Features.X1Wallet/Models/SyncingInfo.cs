using System.Collections.Generic;
using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models
{
    public sealed class SyncingInfo
    {
        public int ConsensusTipHeight;
        public uint256 ConsensusTipHash;
        public int ConsensusTipAge;
        public int BlockStoreHeight;
        public uint256 BlockStoreHash;
        public int MaxTipAge;
        public bool IsAtBestChainTip;
        public int WalletTipHeight;
        public uint256 WalletTipHash;
        public string WalletName;
        public ConnectionInfo ConnectionInfo;
    }

    public sealed class ConnectionInfo
    {
        public int InBound;
        public int OutBound;
        public List<PeerInfo> Peers;
        public int BestPeerHeight;
        public uint256 BestPeerHash;
    }

    public sealed class PeerInfo
    {
        public bool IsInbound;
        public string Version;
        public string RemoteSocketEndpoint;
        public int BestReceivedTipHeight;
        public uint256 BestReceivedTipHash;
    }
}
