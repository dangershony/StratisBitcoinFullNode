using System;
using System.Diagnostics;
using NBitcoin;

namespace Obsidian.Networks.ObsidianX
{
	/// <inheritdoc />
	public class ObsidianXPoSConsensusOptions : PosConsensusOptions
	{
		
        /// <summary>
        /// Initializes all values. Used by networks that use block weight rules.
        /// </summary>
        public ObsidianXPoSConsensusOptions(
            uint maxBlockBaseSize,
            uint maxBlockWeight,
            uint maxBlockSerializedSize,
            int witnessScaleFactor,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost) : base(maxBlockBaseSize, maxBlockWeight, maxBlockSerializedSize, witnessScaleFactor, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost)
        {
        }

        /// <summary>
        /// Initializes values for networks that use block size rules.
        /// </summary>
        public ObsidianXPoSConsensusOptions(
            uint maxBlockBaseSize,
            int maxStandardVersion,
            int maxStandardTxWeight,
            int maxBlockSigopsCost,
            int maxStandardTxSigopsCost,
            int witnessScaleFactor
        ) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost, maxStandardTxSigopsCost, witnessScaleFactor)
        {
        }

        /// <inheritdoc />
        public override int GetStakeMinConfirmations(int height, Network network)
        {
            if (network.IsTest() || network.IsRegTest())
                throw new NotImplementedException();

            Debug.Assert(network.Consensus.MaxReorgLength == 125);

            // StakeMinConfirmations must equal MaxReorgLength so that nobody can stake in isolation and then force a reorg
            return (int)network.Consensus.MaxReorgLength; 
        }
	}
}
