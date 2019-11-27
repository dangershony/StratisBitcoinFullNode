using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks <see cref="ObsidianXMain"/> transaction inputs have empty ScriptSig fields.
    /// </summary>
    public class ObsidianXEmptyScriptSigRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;

            foreach (var tx in block.Transactions)
            {
                if (tx.IsProtocolTransaction())
                    continue;

                foreach (var txin in tx.Inputs)
                {
                    // according to BIP-0141, P2WPKH and P2WSH transaction have an empty ScriptSig,
                    // so let's whitelist these.
                    if ((txin.ScriptSig == null || txin.ScriptSig.Length == 0) && tx.HasWitness)
                        continue;

                    // P2WPKH nested in BIP16 P2SH, P2WSH nested in BIP16 P2SH, P2SH, P2PKH
                    // do not have empty ScriptSig, throw!
                    // If we did not check the ScriptSig is empty, we'd have transaction malleability. Also, scripts may be as long as 10000 bytes,
                    // and the ScriptSig field is not intended to be a convenient storage place.
                    this.Logger.LogTrace("(-)[SCRIPTSIG_NOT_EMPTY]");
                    new ConsensusError("scriptsig-not-empty", "SegWit requires empty ScriptSig fields.").Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}