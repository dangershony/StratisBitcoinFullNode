using System;
using NBitcoin;

namespace Obsidian.Features.X1Wallet.Models.Api.Responses
{
    public class GetWalletInfoResponse
    {
        internal string WalletName;

        public string WalletFilePath { get; set; }

        public Network Network { get; set; }

        public DateTimeOffset CreationTime { get; set; }

        public bool IsDecrypted { get; set; }

        public int? LastBlockSyncedHeight { get; set; }

        public int? ChainTip { get; set; }

        public bool IsChainSynced { get; set; }

        public int ConnectedNodes { get; set; }
    }
}
