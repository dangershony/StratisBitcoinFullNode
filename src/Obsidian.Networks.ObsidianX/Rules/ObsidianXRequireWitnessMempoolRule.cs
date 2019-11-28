using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks weather the transaction has witness.
    /// </summary>
    public class ObsidianXRequireWitnessMempoolRule : MempoolRule
    {
        public ObsidianXRequireWitnessMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (!context.Transaction.HasWitness)
            {
                this.logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXRequireWitnessMempoolRule)}]".ToUpperInvariant());
                ObsidianXConsensusErrors.MissingWitness.Throw();
            }
        }
    }
}