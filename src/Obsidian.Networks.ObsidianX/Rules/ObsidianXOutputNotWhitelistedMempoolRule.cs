﻿using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.MemoryPool;
using Stratis.Bitcoin.Features.MemoryPool.Interfaces;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if transactions match the white-listing criteria. This rule and <see cref="ObsidianXOutputNotWhitelistedRule"/> must correspond.
    /// </summary>
    public class ObsidianXOutputNotWhitelistedMempoolRule : MempoolRule
    {
        public ObsidianXOutputNotWhitelistedMempoolRule(Network network,
            ITxMempool mempool,
            MempoolSettings mempoolSettings,
            ChainIndexer chainIndexer,
            IConsensusRuleEngine consensusRules,
            ILoggerFactory loggerFactory) : base(network, mempool, mempoolSettings, chainIndexer, loggerFactory)
        {
        }

        public override void CheckTransaction(MempoolValidationContext context)
        {
            if (context.Transaction.IsCoinStake || (context.Transaction.IsCoinBase && context.Transaction.Outputs[0].IsEmpty)) // also check the coinbase tx in PoW blocks
                return;

            foreach (var output in context.Transaction.Outputs)
            {
                if (ObsidianXOutputNotWhitelistedRule.IsOutputWhitelisted(output))
                    continue;

                this.logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXOutputNotWhitelistedMempoolRule)}]".ToUpperInvariant());
                context.State.Fail(new MempoolError(ObsidianXConsensusErrors.OutputNotWhitelisted)).Throw();
            }
        }
    }
}
