using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.ColdStaking;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if transactions match the white-listing criteria. This rule and <see cref="ObsidianXOutputNotWhitelistedMempoolRule"/> must correspond.
    /// </summary>
    public class ObsidianXOutputNotWhitelistedRule : PartialValidationConsensusRule
    {
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
                    if (ColdStakingScriptTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                        continue; // allowed cold staking setup trx

                    this.Logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXOutputNotWhitelistedRule)}]".ToUpperInvariant());
                    ObsidianXConsensusErrors.OutputNotWhitelisted.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}