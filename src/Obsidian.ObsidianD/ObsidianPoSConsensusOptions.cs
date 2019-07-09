using System;
using System.Diagnostics;
using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Obsidian.OxD
{
	/// <inheritdoc />
	public class ObsidianPoSConsensusOptions : PosConsensusOptions
	{
		public ObsidianPoSConsensusOptions(uint maxBlockBaseSize,
			int maxStandardVersion,
			int maxStandardTxWeight,
			int maxBlockSigopsCost,
			int maxStandardTxSigopsCost) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost,
			maxStandardTxSigopsCost
		)
		{ }

		/// <inheritdoc />
		public override int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.IsTest() || network.IsRegTest())
                throw new NotImplementedException();

            Debug.Assert(network.Consensus.MaxReorgLength == 500);

            // StakeMinConfirmations must equal MaxReorgLength so that nobody can stake in isolation and then force a reorg
            return (int)network.Consensus.MaxReorgLength; 
        }
	}
}
