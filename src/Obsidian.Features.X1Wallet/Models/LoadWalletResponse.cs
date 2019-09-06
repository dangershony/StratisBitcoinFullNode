using System;
using System.Collections.Generic;
using System.Text;

namespace Obsidian.Features.X1Wallet.Models
{
    public class LoadWalletResponse
    {
        /// <summary>
        /// Format: CipherV2Bytes as HexString.
        /// </summary>
        public string PassphraseChallenge { get; internal set; }
    }
}
