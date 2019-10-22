using NBitcoin;
using NBitcoin.BouncyCastle.Math;

namespace Obsidian.Features.X1Wallet.Staking
{

    public sealed class PosV3
    {
        public long CurrentBlockTime;
        public Target TargetBits;
        public uint256 PreviousStakeModifierV2;
        public int TargetSpacingSeconds;
        public int TargetBlockTime;
        public BigInteger Target;
    }

    public class StakingStatus
    {
        public long StartedUtc;
        public long WaitMs;
        public int KernelsFound;
        public long ComputateTimeMs;
        public string Errors;
        internal int UnspentOutputs;
        internal long Weight;
        internal long Immature;
    }

    public class StakedBlock
    {
        internal int Height;
        internal uint256 Hash;
        internal long Size;
        internal int Transactions;
        internal long TotalReward;
        internal long WeightUsed;
        internal string KernelAddress;
        internal long TotalComputeTimeMs;
        internal long BlockTime;
    }

    public class StakingInfo
    {
        public bool Enabled;
        public PosV3 PosV3;
        public StakingStatus Status;
        public StakedBlock LastStakedBlock;
        public long NetworkWeight;
        public long ExpectedTime;
    }
}
