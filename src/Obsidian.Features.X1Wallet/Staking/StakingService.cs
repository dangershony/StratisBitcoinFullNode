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
        public readonly StakedBlock LastStakedBlock;
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
            this.PosV3 = new PosV3 { SearchInterval = 64, BlockInterval = 4 * 64 };
            this.LastStakedBlock = new StakedBlock();
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

                        this.stopwatch.Stop();
                    }
                    else
                    {
                        Wait(previousBlockTime);
                    }
                }
                catch (Exception e)
                {
                    this.Status.LastException = e.Message.Replace(":", "-");
                    this.Status.Exceptions++;
                    this.logger.LogWarning($"Staking Loop: {e.Message}");
                }
            }
        }

        void Wait(long previousBlockTime)
        {
            long currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long nextStartMs = (previousBlockTime + this.PosV3.SearchInterval) * 1000;
            this.Status.WaitMs = nextStartMs - currentMs;
            if (this.Status.WaitMs <= 0)
                return;

            Task.Delay((int)this.Status.WaitMs).Wait(this.cts.Token);
        }

        void Stake()
        {
            this.stopwatch.Restart();
            BlockTemplate blockTemplate = GetBlockTemplate();

            this.PosV3.TargetDifficulty = blockTemplate.Block.Header.Bits.Difficulty;

            this.Status.NetworkWeight = GetNetworkWeight();
            this.Status.ExpectedTime = GetExpectedTime(this.Status.NetworkWeight, out this.Status.WeightPercent);

            this.PosV3.TargetAsBigInteger = blockTemplate.Block.Header.Bits.ToBigInteger(); // for calculation
            this.PosV3.Target = blockTemplate.Block.Header.Bits.ToUInt256(); // same for display only
            this.PosV3.StakeModifierV2 = GetStakeModifierV22();

            var coins = GetUnspentOutputs();
            var validKernels = FindValidKernels(coins);

            this.Status.KernelsFound = validKernels.Count;
            this.Status.ComputeTimeMs = this.stopwatch.ElapsedMilliseconds;

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
            BigInteger weightedTarget = this.PosV3.TargetAsBigInteger.Multiply(value);

            uint256 kernelHash;
            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(this.PosV3.StakeModifierV2);
                serializer.ReadWrite(stakingCoin.Time);
                serializer.ReadWrite(stakingCoin.Outpoint.Hash);
                serializer.ReadWrite(stakingCoin.Outpoint.N);
                serializer.ReadWrite((uint)this.PosV3.CurrentBlockTime); // be sure it's serialized as uint
                kernelHash = Hashes.Hash256(ms.ToArray());
            }


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

            ChainedHeader chainedHeader;
            try
            {
                chainedHeader = this.consensusManager.BlockMinedAsync(blockTemplate.Block).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                this.Status.BlocksNotAccepted++;
                throw;
            }

            if (chainedHeader == null)
            {
                this.Status.BlocksNotAccepted += 1;
                return;
            }

            if (this.LastStakedBlock.BlockTime != 0)
            {
                this.Status.ActualTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.LastStakedBlock.BlockTime);
            }
            else
            {
                this.Status.ActualTime = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - this.Status.StartedUtc);
            }

            this.LastStakedBlock.Hash = chainedHeader.HashBlock;
            this.LastStakedBlock.Height = chainedHeader.Height;
            this.LastStakedBlock.BlockSize = chainedHeader.Block.BlockSize ?? -1;
            this.LastStakedBlock.TxId = chainedHeader.Block.Transactions[1].GetHash();
            this.LastStakedBlock.Transactions = chainedHeader.Block.Transactions.Count;
            this.LastStakedBlock.TotalReward = totalReward;
            this.LastStakedBlock.KernelAddress = kernelCoin.Address;
            this.LastStakedBlock.WeightUsed = kernelCoin.Amount;
            this.LastStakedBlock.TotalComputeTimeMs = this.stopwatch.ElapsedMilliseconds;
            this.LastStakedBlock.BlockTime = this.PosV3.CurrentBlockTime;
            this.Status.BlocksAccepted += 1;

            this.logger.LogInformation(
                $"Congratulations, your staked a new block at height {newBlockHeight} and received a total reward of {totalReward} {this.network.CoinTicker}.");
        }

        double GetNetworkWeight()
        {
            var result = this.PosV3.TargetDifficulty * 0x100000000;
            if (result > 0)
            {
                result /= this.PosV3.BlockInterval;
                result *= this.PosV3.SearchInterval;
                return result;
            }
            return 0;
        }


        int GetExpectedTime(double networkWeight, out double ownPercent)
        {
            if (this.Status.Weight <= 0)
            {
                ownPercent = 0;
                return int.MaxValue;
            }

            var ownWeight = (double)this.Status.Weight;

            var ownFraction = ownWeight / networkWeight;

            var expectedTimeSeconds = this.PosV3.BlockInterval / ownFraction;
            ownPercent = Math.Round(ownFraction * 100, 1);

            return (int)(this.PosV3.BlockInterval + expectedTimeSeconds);
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
                this.Status.Weight = balance.Stakable;
                this.Status.Immature = balance.Confirmed - balance.Stakable;

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
            long blockTime = currentTime - currentTime % this.PosV3.SearchInterval;
            return blockTime;
        }
    }


}
