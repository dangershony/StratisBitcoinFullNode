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
    public class ObsidianXOutputNotWhitelistedMempoolRule : MempoolRule
    {
        readonly IConsensusRuleEngine consensusRules;

        public ObsidianXOutputNotWhitelistedMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
            this.consensusRules = consensusRules;
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.IsCoinStake || (context.Transaction.IsCoinBase && context.Transaction.Outputs[0].IsEmpty)) // also check the coinbase tx in PoW blocks
                return;

            foreach (var output in context.Transaction.Outputs)
            {

                if (PayToWitTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                    continue; // allowed are P2WPKH and P2WSH
                if (TxNullDataTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                    continue; // allowed are also all kinds of valid OP_RETURN pushes
                if (ColdStakingScriptTemplate.Instance.CheckScriptPubKey(output.ScriptPubKey))
                    continue; // allowed cold staking setup trx

                this.logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXOutputNotWhitelistedMempoolRule)}]".ToUpperInvariant());
                context.State.Fail(new MempoolError(ObsidianXConsensusErrors.OutputNotWhitelisted)).Throw();
            }
        }
    }
}