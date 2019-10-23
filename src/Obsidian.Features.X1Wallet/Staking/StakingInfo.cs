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
        public int UnspentOutputs;
        public long Weight;
        public long Immature;
    }

    public class StakedBlock
    {
        public int Height;
        public uint256 Hash;
        public long Size;
        public int Transactions;
        public long TotalReward;
        public long WeightUsed;
        public string KernelAddress;
        public long TotalComputeTimeMs;
        public long BlockTime;
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
