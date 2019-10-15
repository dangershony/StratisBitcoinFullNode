using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Rules;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if <see cref="ObsidianXMain"/> network block's header has a valid block version.
    /// </summary>
    public class ObsidianXPosCoinviewRule : PosCoinviewRule
    {
        /// <inheritdoc />
        public override Money GetProofOfWorkReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensus.PremineReward;

            return this.consensus.ProofOfWorkReward;
        }

        /// <inheritdoc />
        public override Money GetProofOfStakeReward(int height)
        {
            if (this.IsPremine(height))
                return this.consensus.PremineReward;

            // During the economic recovery the POS block reward is set to 50 ODX.
            if (height <= 240_000)
            {
                return new Money(50, MoneyUnit.BTC);
            }

            return this.consensus.ProofOfStakeReward;
        }
    }
}