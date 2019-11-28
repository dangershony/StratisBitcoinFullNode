using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.ColdStaking;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if transactions match the white-listing criteria. This rule and <see cref="ObsidianXOutputNotWhitelistedRule"/> must correspond.
    /// </summary>
    public class ObsidianXEmptyScriptSigMempoolRule : MempoolRule
    {
        public ObsidianXEmptyScriptSigMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.IsCoinBase)
                return;

            foreach (var txin in context.Transaction.Inputs)
            {
                // According to BIP-0141, P2WPKH and P2WSH transaction must have an empty ScriptSig,
                // which is what we require to let a tx pass. The requirement's scope includes
                // Coinstake transactions as well as standard transactions.
                if ((txin.ScriptSig == null || txin.ScriptSig.Length == 0) && context.Transaction.HasWitness)
                    continue;

                this.logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXEmptyScriptSigMempoolRule)}]".ToUpperInvariant());
                ObsidianXConsensusErrors.ScriptSigNotEmpty.Throw();
            }
        }
    }
}