using Stratis.Bitcoin.Consensus;

namespace Obsidian.Networks.ObsidianX.Rules
{
    public static class ObsidianXConsensusErrors
    {
        public static ConsensusError OutputNotWhitelisted => new ConsensusError("tx-output-not-whitelisted", "Only P2WPKH, P2WSH, OP_RETURN and CS_SETUP is allowed outside protocol transactions.");
    }
}
