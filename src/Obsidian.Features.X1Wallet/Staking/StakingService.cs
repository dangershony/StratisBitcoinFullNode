using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Mining;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Staking
{
    sealed class StakingService
    {
        public readonly StakingStatus Status;
        public readonly PosV3 PosV3;
        public readonly StakedBlock StakedBlock;
        readonly Task stakingTask;
        readonly CancellationTokenSource cts;
        readonly ILogger logger;
        readonly WalletManager walletManager;
        readonly Network network;
        readonly IBlockProvider blockProvider;
        readonly IConsensusManager consensusManager;
        readonly ChainIndexer chainIndexer;
        readonly string passphrase;
        readonly PosCoinviewRule posCoinviewRule;
        readonly Stopwatch stopwatch;
        readonly IStakeChain stakeChain;

        public StakingService(WalletManager walletManager, string passphrase, ILoggerFactory loggerFactory, Network network, IBlockProvider blockProvider, IConsensusManager consensusManager, ChainIndexer chainIndexer, IStakeChain stakeChain)
        {
            this.cts = new CancellationTokenSource();
            this.stakingTask = new Task(StakingLoop, this.cts.Token);
            this.walletManager = walletManager;
            this.passphrase = passphrase;
            this.logger = loggerFactory.CreateLogger(typeof(StakingService).FullName);
            this.network = network;
            this.blockProvider = blockProvider;
            this.consensusManager = consensusManager;
            this.chainIndexer = chainIndexer;
            this.posCoinviewRule = this.consensusManager.ConsensusRules.GetRule<PosCoinviewRule>();
            this.stopwatch = Stopwatch.StartNew();
            this.stakeChain = stakeChain;
            this.Status = new StakingStatus { StartedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            this.PosV3 = new PosV3 { TargetSpacingSeconds = 64, TargetBlockTime = 4 * 64 };
            this.StakedBlock = new StakedBlock();
        }

        public void Start()
        {
            if (this.stakingTask.Status != TaskStatus.Running)
            {
                this.stakingTask.Start();
            }
        }

        public void Stop()
        {
            if (!this.cts.IsCancellationRequested)
            {
                this.cts.Cancel();
            }
        }

        void StakingLoop()
        {
            long previousBlockTime = 0;

            while (!this.cts.IsCancellationRequested)
            {
                try
                {
                    this.PosV3.CurrentBlockTime = GetCurrentBlockTime();

                    if (this.PosV3.CurrentBlockTime > previousBlockTime)
                    {
                        previousBlockTime = this.PosV3.CurrentBlockTime;

                        this.stopwatch.Restart();

                        Stake();

                        this.Status.ComputateTimeMs = this.stopwatch.ElapsedMilliseconds;
                        this.stopwatch.Stop();
                    }
                    else
                    {
                        Wait(previousBlockTime);
                    }
                }
                catch (Exception e)
                {
                    this.logger.LogWarning($"Staking Error: {e.Message}");
                }
            }
        }

        void Wait(long previousBlockTime)
        {
            long currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long nextStartMs = (previousBlockTime + this.PosV3.TargetSpacingSeconds) * 1000;
            this.Status.WaitMs = nextStartMs - currentMs;
            if (this.Status.WaitMs <= 0)
                return;

            Task.Delay((int)this.Status.WaitMs).Wait(this.cts.Token);
        }

        void Stake()
        {
            this.stopwatch.Restart();
            BlockTemplate blockTemplate = GetBlockTemplate();

            this.PosV3.Target = blockTemplate.Block.Header.Bits;
            this.PosV3.PreviousStakeModifierV2 = GetStakeModifierV22();

            var coins = GetUnspentOutputs();
            var validKernels = FindValidKernels(coins);

            this.Status.KernelsFound = validKernels.Count;
            this.Status.ComputateTimeMs = this.stopwatch.ElapsedMilliseconds;

            if (validKernels.Count > 0)
                CreateNextBlock(blockTemplate, validKernels);
        }

        uint256 GetStakeModifierV22()
        {
            return this.stakeChain.Get(this.chainIndexer.Tip.HashBlock).StakeModifierV2;
        }

        uint256 GetStakeModifierV2()
        {
            var last = this.chainIndexer.Tip;

            search:
            if (last.Header is ProvenBlockHeader proven)
            {
                return proven.StakeModifierV2;
            }

            last = last.Previous;
            if (last.Height == 0)
                return uint256.Zero;
            goto search;
        }

        List<StakingCoin> FindValidKernels(StakingCoin[] coins)
        {
            var validKernels = new List<StakingCoin>();
            foreach (var c in coins)
            {
                if (CheckStakeKernelHash(c))
                    validKernels.Add(c);
            }
            return validKernels;
        }

        bool CheckStakeKernelHash(StakingCoin stakingCoin)
        {
            var value = BigInteger.ValueOf(stakingCoin.Amount.Satoshi);
            BigInteger weightedTarget = this.PosV3.Target.ToBigInteger().Multiply(value);

            using var ms = new MemoryStream();

            var serializer = new BitcoinStream(ms, true);
            serializer.ReadWrite(this.PosV3.PreviousStakeModifierV2);
            serializer.ReadWrite(stakingCoin.Time);
            serializer.ReadWrite(stakingCoin.Outpoint.Hash);
            serializer.ReadWrite(stakingCoin.Outpoint.N);
            serializer.ReadWrite(this.PosV3.CurrentBlockTime);

            uint256 kernelHash = Hashes.Hash256(ms.ToArray());
            var hash = new BigInteger(1, kernelHash.ToBytes(false));

            return hash.CompareTo(weightedTarget) <= 0;
        }

        BlockTemplate GetBlockTemplate()
        {
            return this.blockProvider.BuildPosBlock(this.consensusManager.Tip, new Script());
        }

        void CreateNextBlock(BlockTemplate blockTemplate, List<StakingCoin> kernelCoins)
        {
            StakingCoin kernelCoin = kernelCoins[0];
            foreach (var coin in kernelCoins)
                if (coin.Amount < kernelCoin.Amount)
                    kernelCoin = coin;

            var newBlockHeight = this.chainIndexer.Tip.Height + 1;

            var totalReward = blockTemplate.TotalFee + this.posCoinviewRule.GetProofOfStakeReward(newBlockHeight);

            var key = new Key(VCL.DecryptWithPassphrase(this.passphrase, kernelCoin.EncryptedPrivateKey));

            Transaction tx = new PosTransaction();

            tx.Time = blockTemplate.Block.Header.Time = blockTemplate.Block.Transactions[0].Time = (uint)this.PosV3.CurrentBlockTime;

            tx.AddInput(new TxIn(kernelCoin.Outpoint));

            tx.Outputs.Add(new TxOut(0, Script.Empty));
            tx.Outputs.Add(new TxOut(0, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(key.PubKey.Compress().ToBytes()))));
            tx.Outputs.Add(new TxOut(totalReward + kernelCoin.Amount, kernelCoin.ScriptPubKey));

            tx.Sign(this.network, new[] { key }, new ICoin[] { kernelCoin });

            blockTemplate.Block.Transactions.Insert(1, tx);

            this.blockProvider.BlockModified(this.consensusManager.Tip, blockTemplate.Block);

            ECDSASignature signature = key.Sign(blockTemplate.Block.GetHash());
            ((PosBlock)blockTemplate.Block).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            ChainedHeader chainedHeader = this.consensusManager.BlockMinedAsync(blockTemplate.Block).GetAwaiter().GetResult();

            this.StakedBlock.Hash = chainedHeader.HashBlock;
            this.StakedBlock.Height = chainedHeader.Height;
            this.StakedBlock.Size = chainedHeader.Block.BlockSize ?? -1;
            this.StakedBlock.Transactions = chainedHeader.Block.Transactions.Count;
            this.StakedBlock.TotalReward = totalReward;
            this.StakedBlock.KernelAddress = kernelCoin.Address;
            this.StakedBlock.WeightUsed = kernelCoin.Amount;
            this.StakedBlock.TotalComputeTimeMs = this.stopwatch.ElapsedMilliseconds;
            this.StakedBlock.BlockTime = this.PosV3.CurrentBlockTime;

            this.logger.LogInformation($"Congratulations, you staked a new block at height {newBlockHeight} and received a reward of {totalReward} {this.network.CoinTicker}.");

        }

        public long GetNetworkWeight()
        {
            var result = this.PosV3.Target.Difficulty * 0x100000000;
            if (result > 0)
            {
                result /= this.PosV3.TargetBlockTime;
                result *= this.PosV3.TargetSpacingSeconds;
                return (long) result;
            }
            return 0;
        }

        public long GetExpectedTime()
        {
            if (this.Status.Weight > 0)
                return GetNetworkWeight() * this.PosV3.TargetBlockTime / this.Status.Weight;
            return long.MaxValue;
        }

        ChainedHeader GetLastPosHeader()
        {
            ChainedHeader header = this.consensusManager.Tip;

            while (header != null && header.Height > 0)
            {
                BlockStake blockStake = this.stakeChain.Get(header.HashBlock);

                if (blockStake != null && blockStake.IsProofOfStake())
                    return header;

                header = header.Previous;
            }
            return null;
        }

        StakingCoin[] GetUnspentOutputs()
        {
            try
            {
                this.walletManager.WalletSemaphore.Wait();

                var coins = this.walletManager.GetBudget(out var balance, true);

                this.Status.UnspentOutputs = coins.Length;
                this.Status.Weight = balance.SpendableAmount.Satoshi;
                this.Status.Immature = balance.AmountConfirmed.Satoshi - balance.SpendableAmount.Satoshi;

                return coins;
            }
            finally
            {
                this.walletManager.WalletSemaphore.Release();
            }

        }

        long GetCurrentBlockTime()
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long blockTime = currentTime - currentTime % this.PosV3.TargetSpacingSeconds;
            return blockTime;
        }
    }


}
