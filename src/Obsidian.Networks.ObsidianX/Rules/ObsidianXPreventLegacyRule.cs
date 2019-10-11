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
    public class ObsidianXPreventLegacyRule : PartialValidationConsensusRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated or otherwise invalid.</exception>
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;
            if (block.Transactions.Count < 2)
                return Task.CompletedTask;

            if (block.Transactions[1].IsCoinStake && block.Transactions[1].Outputs.Count != 3)
            {
                this.Logger.LogTrace("(-)[LEGACY_COINSSTAKE]");
                new ConsensusError("legacy-coinstake", "Expected three outputs in coinstake tx but found two.").Throw();
            }
            else
            {
                CheckForP2Pk(block.Transactions[1]);
            }

            for (var i = 2; i < block.Transactions.Count; i++)
            {
                CheckForP2Pk(block.Transactions[i]);
            }

            return Task.CompletedTask;
        }

        void CheckForP2Pk(Transaction transaction)
        {
            foreach (var output in transaction.Outputs)
            {
                var pubKey = output.ScriptPubKey.GetDestinationPublicKeys(null);
                if (pubKey.Length > 0)
                {
                    this.Logger.LogTrace("(-)[LEGACY_TX]");
                    new ConsensusError("legacy-p2pk", "P2PK outputs are not allowed.").Throw();
                }
            }
        }
    }
}