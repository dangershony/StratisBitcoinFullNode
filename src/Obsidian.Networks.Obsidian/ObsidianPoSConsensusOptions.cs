using NBitcoin;

namespace Obsidian.Networks.Obsidian
{
	/// <inheritdoc />
	public class ObsidianPoSConsensusOptions : PosConsensusOptions
	{
		/// <summary>Coinstake minimal confirmations softfork activation height for mainnet.</summary>
		const int ObsidianCoinstakeMinConfirmationActivationHeightMainnet = int.MaxValue;

		/// <summary>Coinstake minimal confirmations softfork activation height for testnet.</summary>
		const int ObsidianCoinstakeMinConfirmationActivationHeightTestnet = 436000;  // TODO: Create testnet for Obsidian

		public ObsidianPoSConsensusOptions(uint maxBlockBaseSize,
			int maxStandardVersion,
			int maxStandardTxWeight,
			int maxBlockSigopsCost,
			int maxStandardTxSigopsCost, int witnessScaleFactor) : base(maxBlockBaseSize, maxStandardVersion, maxStandardTxWeight, maxBlockSigopsCost,
			maxStandardTxSigopsCost, witnessScaleFactor
		)
		{ }

		/// <inheritdoc />
		public override int GetStakeMinConfirmations(int height, Network network)
		{
			if (network.IsTest())
				return height < ObsidianCoinstakeMinConfirmationActivationHeightTestnet ? 10 : 20;

			return height < ObsidianCoinstakeMinConfirmationActivationHeightMainnet ? 50 : 500;
		}
	}
}
