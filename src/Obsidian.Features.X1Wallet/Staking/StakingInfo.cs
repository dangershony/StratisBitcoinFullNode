using NBitcoin;
using NBitcoin.BouncyCastle.Math;

namespace Obsidian.Features.X1Wallet.Staking
{

    public sealed class PosV3
    {
        public long CurrentBlockTime;  // used to calculate kernel
        
        public int SearchInterval; // used to calculate CurrentBlockTime
        public int BlockInterval;  // used to calculate expected time
                   
        internal BigInteger TargetAsBigInteger; // used to calculate kernel (internal so that it doesn't get serialized)
                       
        public uint256 StakeModifierV2; // used to calculate kernel
        public uint256 Target; // same as TargetAsBigInteger for display only
        public double TargetDifficulty;  // same as TargetAsBigInteger for display, expected time, network weight
    }

    public sealed class StakingStatus
    {
        // Values since staking started
        public long StartedUtc;
        public int BlocksAccepted;
        public int BlocksNotAccepted;

        public int Exceptions;
        public string LastException;

        // Values in one slot
        public long WaitMs;
        public long ComputeTimeMs;
        public int KernelsFound;
       
        public int UnspentOutputs;

        public long Immature;
        public long Weight;
        public double NetworkWeight;
        public double WeightPercent;
        public int ExpectedTime;
        public int ActualTime;
    }

    public sealed class StakedBlock
    {
        public int Height;
        public uint256 Hash;
        public uint256 TxId;
        public long BlockTime;
        public long TotalReward;
        public long BlockSize;
        public int Transactions;
       
        public long WeightUsed;
        public string KernelAddress;
        public long TotalComputeTimeMs;
    }

    public sealed class StakingInfo
    {
        public bool Enabled;
        public PosV3 PosV3;
        public StakingStatus StakingStatus;
        public StakedBlock LastStakedBlock;
    }
}
