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
    public class ObsidianXHeaderVersionRule : HeaderVersionRule
    {
        /// <inheritdoc />
        /// <exception cref="ConsensusErrors.BadVersion">Thrown if block's version is outdated or otherwise invalid.</exception>
        public override void Run(RuleContext context)
        {
            Guard.NotNull(context.ValidationContext.ChainedHeaderToValidate, nameof(context.ValidationContext.ChainedHeaderToValidate));

            ChainedHeader chainedHeader = context.ValidationContext.ChainedHeaderToValidate;

            // ODX will always use BIP9 enabled blocks.
            if ((chainedHeader.Header.Version & ThresholdConditionCache.VersionbitsTopMask) != ThresholdConditionCache.VersionbitsTopBits)
            {
                this.Logger.LogTrace("(-)[BAD_VERSION]");
                ConsensusErrors.BadVersion.Throw();
            }
        }
    }
}