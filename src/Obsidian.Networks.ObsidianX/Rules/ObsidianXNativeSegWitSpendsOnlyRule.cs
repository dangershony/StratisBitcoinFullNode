using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if <see cref="ObsidianXMain"/> network's blocks confirm to the 'native-SegWit-only' white-listing criteria.
    /// </summary>
    public class ObsidianXNativeSegWitSpendsOnlyRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrorException">Thrown if a block's transactions confirm to the 'native-SegWit-only' white-listing criteria.</exception>
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;
            var isPosBlock = block.Transactions.Count >= 2 && block.Transactions[1].IsCoinStake;

            foreach (var transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                if (transaction.IsCoinStake)
                    continue;

                if (transaction.IsCoinBase && isPosBlock)
                    continue; // do not check the coinbase tx in a PoS block

                foreach (var output in transaction.Outputs)
                {
                   
                    if (PayToWitTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                        continue; // allowed are P2WPKH and P2WSH
                    if (TxNullDataTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                        continue; // allowed are also all kinds of valid OP_RETURN pushes

                    this.Logger.LogTrace("(-)[NOT_NATIVE_SEGWIT_OR_DATA]");
                    new ConsensusError("legacy-tx", "Only P2WPKH, P2WSH is allowed outside Coinstake transactions.").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}