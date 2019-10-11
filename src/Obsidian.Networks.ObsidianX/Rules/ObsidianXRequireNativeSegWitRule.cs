using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if <see cref="ObsidianXMain"/> transaction are only native SegWit.
    /// </summary>
    public class ObsidianXRequireNativeSegWitRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;

            foreach (var tx in block.Transactions)
            {
                if (tx.IsCoinStake) // Coinstake tx are custom to ObsidianX, skip
                    continue;

                if (!tx.HasWitness)
                {
                    this.Logger.LogTrace("(-)[MISSING_WITNESS]");
                    new ConsensusError("missing-witness", "Missing Witness in Non-Coinstake Transaction.").Throw();
                }

                if (!tx.IsCoinBase)
                {
                    foreach (var txin in tx.Inputs)
                    {
                        // according to BIP-0141, P2WPKH and P2WSH transaction have an empty ScriptSig,
                        // so let's whitelist these.
                        if (txin.ScriptSig.Length == 0)
                            continue;
                        // P2WPKH nested in BIP16 P2SH, P2WSH nested in BIP16 P2SH, P2SH, P2PKH
                        // do not have empty ScriptSig, throw!
                        // This also means that only native SegWit outputs are spendable, because spending other outputs would require a ScriptSig.
                        this.Logger.LogTrace("(-)[NONEMPTY_ScriptSig]");
                        new ConsensusError("non-empty-scriptsig", "Only native SegWit (P2WPKH and P2WSH) is allowed.").Throw();
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}