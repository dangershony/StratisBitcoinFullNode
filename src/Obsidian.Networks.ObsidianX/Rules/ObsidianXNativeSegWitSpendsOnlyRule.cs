using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if <see cref="ObsidianXMain"/> network's blocks contain legacy coinstake tx or P2PK outputs.
    /// </summary>
    public class ObsidianXNativeSegWitSpendsOnlyRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated or otherwise invalid.</exception>
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;

            for (var i = 0; i < block.Transactions.Count; i++)
            {
                if (i == 1) // do not check the Coinstake tx
                    continue;

                var transaction = block.Transactions[i];

                foreach (var output in transaction.Outputs)
                {
                    if (PayToWitTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                        continue; // allowed are P2WPKH and P2WSH
                    if (TxNullDataTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                        continue; // allowed are also all kinds of valid OP_RETURN pushes

                    this.Logger.LogTrace("(-)[NOT_NATIVE_SEGWIT_OR_DATA]");
                    new ConsensusError("legacy-tx", "Only P2PKH, P2WSH is allowed outside Coinstake transactions.").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}