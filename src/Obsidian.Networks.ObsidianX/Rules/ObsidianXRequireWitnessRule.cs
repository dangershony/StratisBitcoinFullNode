using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if all transaction in the block have witness.
    /// </summary>
    public class ObsidianXRequireWitnessRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;

            foreach (var tx in block.Transactions)
            {
                if (!tx.HasWitness)
                {
                    this.Logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXRequireWitnessRule)}]".ToUpperInvariant());
                    ObsidianXConsensusErrors.MissingWitness.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}