namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class LoadWalletResponse
    {
        /// <summary>
        /// Format: CipherV2Bytes as HexString.
        /// </summary>
        public string PassphraseChallenge { get; internal set; }
    }
}
