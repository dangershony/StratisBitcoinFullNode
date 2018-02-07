using NBitcoin;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.BlockStore
{
    /// <summary>
    /// Structure made of a block and its chained header.
    /// </summary>
    public sealed class BlockPair
    {
        /// <summary>The block.</summary>
        public PowBlock PowBlock { get; private set; }

        /// <summary>Chained header of the <see cref="PowBlock"/>.</summary>
        public ChainedBlock ChainedBlock { get; private set; }

        /// <summary>
        /// Creates instance of <see cref="BlockPair" />.
        /// </summary>
        /// <param name="powBlock">The block.</param>
        /// <param name="chainedBlock">Chained header of the <paramref name="powBlock"/>.</param>
        public BlockPair(PowBlock powBlock, ChainedBlock chainedBlock)
        {
            Guard.NotNull(powBlock, nameof(powBlock));
            Guard.NotNull(chainedBlock, nameof(chainedBlock));
            Guard.Assert(powBlock.GetHash() == chainedBlock.HashBlock);

            this.PowBlock = powBlock;
            this.ChainedBlock = chainedBlock;
        }
    }
}
