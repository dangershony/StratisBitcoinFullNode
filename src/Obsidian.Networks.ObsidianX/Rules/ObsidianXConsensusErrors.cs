using Stratis.Bitcoin.Consensus;

namespace Obsidian.Networks.ObsidianX.Rules
{
    public static class ObsidianXConsensusErrors
    {
        public static ConsensusError OutputNotWhitelisted => new ConsensusError("tx-output-not-whitelisted", "Only P2WPKH, P2WSH and OP_RETURN are allowed outputs.");

        public static ConsensusError MissingWitness => new ConsensusError("tx-input-missing-witness", "All transaction inputs must have a non-empty WitScript.");

        public static ConsensusError ScriptSigNotEmpty => new ConsensusError("scriptsig-not-empty", "The ScriptSig must be empty.");
    }
}
