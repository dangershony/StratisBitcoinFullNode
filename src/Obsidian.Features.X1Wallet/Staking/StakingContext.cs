using NBitcoin;

namespace Obsidian.Features.X1Wallet.Staking
{
    /// <summary>
    /// Information about coinstake transaction and its private key.
    /// </summary>
    public class StakingContext
    {
        /// <summary>Coinstake transaction being constructed.</summary>
        public Transaction CoinstakeTx { get; set; }
        
        /// <summary>If the function succeeds, this is filled with private key for signing the coinstake kernel.</summary>
        public Key Key { get; set; }
    }
}