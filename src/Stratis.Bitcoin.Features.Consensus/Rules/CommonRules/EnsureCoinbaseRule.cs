using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace Stratis.Bitcoin.Features.Consensus.Rules.CommonRules
{
    public class EnsureCoinbaseRule : ConsensusRule
    {
        /// <inheritdoc />
        public override Task RunAsync(RuleContext context)
        {
            PowBlock powBlock = context.BlockValidationContext.PowBlock;

            // First transaction must be coinbase, the rest must not be
            if ((powBlock.Transactions.Count == 0) || !powBlock.Transactions[0].IsCoinBase)
            {
                this.Logger.LogTrace("(-)[NO_COINBASE]");
                ConsensusErrors.BadCoinbaseMissing.Throw();
            }

            for (int i = 1; i < powBlock.Transactions.Count; i++)
            {
                if (powBlock.Transactions[i].IsCoinBase)
                {
                    this.Logger.LogTrace("(-)[MULTIPLE_COINBASE]");
                    ConsensusErrors.BadMultipleCoinbase.Throw();
                }
            }

            return Task.CompletedTask;
        }
    }
}