using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Staking
{
    class StakingService
    {
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
            this.stakingTask = new Task(DoWork, this.cts.Token);
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
        }



        public void Start()
        {
            if (this.stakingTask.Status != TaskStatus.Running)
            {
                this.logger.LogInformation($"Status is {this.stakingTask.Status}, starting...");
                this.stakingTask.Start();
            }
            else
            {
                this.logger.LogWarning($"Status is {this.stakingTask.Status}, ignoring Start command.");
            }

        }

        public void Stop()
        {
            if (!this.cts.IsCancellationRequested)
            {
                this.logger.LogInformation($"Status is {this.stakingTask.Status}, cancelling...");
                this.cts.Cancel();
            }
        }

        void DoWork()
        {
            try
            {
                long previousBlockTime = 0;

                while (!this.cts.IsCancellationRequested)
                {
                    long currentBlockTime = GetCurrentBlockTime();
                    if (currentBlockTime > previousBlockTime)
                    {
                        previousBlockTime = currentBlockTime;
                        Stake(currentBlockTime);
                    }
                    else
                    {
                        Wait(previousBlockTime);
                    }
                }
            }
            finally
            {
                // Dispose
            }

        }

        void Wait(long previousBlockTime)
        {
            long currentMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long nextStartMs = (previousBlockTime + 64) * 1000;
            long waitMs = nextStartMs - currentMs;
            if (waitMs <= 0)
                return;

            Task.Delay((int)waitMs).Wait(this.cts.Token);
            this.logger.LogInformation($"Resuming after waiting for {waitMs} ms...");

        }

        void Stake(long currentBlockTime)
        {
            try
            {
                this.stopwatch.Restart();
                BlockTemplate blockTemplate = GetBlockTemplate();

                BigInteger target = blockTemplate.Block.Header.Bits.ToBigInteger();
                uint256 prevStakeModifierV2 = GetStakeModifierV22();

                this.logger.LogInformation($"Staking, blocktime is {currentBlockTime}, target: {target}");

                var coins = GetOutputs();
                var validKernels = FindValidKernels(coins, (uint)currentBlockTime, target, prevStakeModifierV2);
                this.logger.LogWarning($"Found {validKernels.Count} solutions in {this.stopwatch.ElapsedMilliseconds} ms.");

                if (validKernels.Count > 0)
                    CreateNextBlock((uint)currentBlockTime, blockTemplate, validKernels);
            }
            catch (Exception e)
            {
                this.logger.LogWarning($"Staking Error: {e.Message}");
            }
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

        static Dictionary<uint256, StakingCoin> FindValidKernels(StakingCoin[] coins, uint currentBlockTime, BigInteger target, uint256 prevStakeModifier)
        {
            var validKernels = new Dictionary<uint256, StakingCoin>();
            foreach (var c in coins)
            {
                if (CheckStakeKernelHash(c, target, prevStakeModifier, currentBlockTime, out uint256 kernelHash))
                    validKernels.Add(kernelHash, c);
            }
            return validKernels;
        }

        static bool CheckStakeKernelHash(StakingCoin stakingCoin, BigInteger target, uint256 prevStakeModifier, uint transactionTime, out uint256 kernelHash)
        {
            BigInteger value = BigInteger.ValueOf(stakingCoin.Amount.Satoshi);
            BigInteger weightedTarget = target.Multiply(value);

            using (var ms = new MemoryStream())
            {
                var serializer = new BitcoinStream(ms, true);
                serializer.ReadWrite(prevStakeModifier);
                serializer.ReadWrite(stakingCoin.Time);
                serializer.ReadWrite(stakingCoin.Outpoint.Hash);
                serializer.ReadWrite(stakingCoin.Outpoint.N);
                serializer.ReadWrite(transactionTime);

                kernelHash = Hashes.Hash256(ms.ToArray());
            }

            var hash = new BigInteger(1, kernelHash.ToBytes(false));

            return hash.CompareTo(weightedTarget) <= 0;
        }



        BlockTemplate GetBlockTemplate()
        {
            return this.blockProvider.BuildPosBlock(this.consensusManager.Tip, new Script());
        }

        void CreateNextBlock(uint currentBlockTime, BlockTemplate blockTemplate, Dictionary<uint256, StakingCoin> validKernels)
        {
            StakingCoin stakingCoin = null;
            uint256 hash;
            long totalIn = long.MaxValue;
            foreach (var solution in validKernels)
            {
                if (solution.Value.Amount < totalIn)
                {
                    hash = solution.Key;
                    stakingCoin = solution.Value;
                    totalIn = solution.Value.Amount;
                }
            }

            var newBlockHeight = this.chainIndexer.Tip.Height + 1;

            var totalReward = blockTemplate.TotalFee + this.posCoinviewRule.GetProofOfStakeReward(newBlockHeight);

            var key = new Key(VCL.DecryptWithPassphrase(this.passphrase, stakingCoin.EncryptedPrivateKey));

            var tx = (PosTransaction)this.network.CreateTransaction();
            tx.Outputs.Clear();
            tx.Inputs.Clear();
            tx.Time = blockTemplate.Block.Header.Time = blockTemplate.Block.Transactions[0].Time = currentBlockTime;

            tx.AddInput(new TxIn(stakingCoin.Outpoint));
            //tx.SignInput(this.network, key, stakingCoin);

            tx.Outputs.Add(new TxOut(0, Script.Empty));
            tx.Outputs.Add(new TxOut(0, new Script(OpcodeType.OP_RETURN, Op.GetPushOp(key.PubKey.Compress().ToBytes()))));
            tx.Outputs.Add(new TxOut(totalReward + totalIn, stakingCoin.ScriptPubKey));

            tx.Sign(this.network, new[] { key }, new[] { stakingCoin });

            blockTemplate.Block.Transactions.Insert(1, tx);

            this.blockProvider.BlockModified(this.consensusManager.Tip, blockTemplate.Block);

            ECDSASignature signature = key.Sign(blockTemplate.Block.GetHash());
            ((PosBlock)blockTemplate.Block).BlockSignature = new BlockSignature { Signature = signature.ToDER() };

            this.consensusManager.BlockMinedAsync(blockTemplate.Block).GetAwaiter().GetResult();

            this.logger.LogInformation($"Congratulations, you staked a new block at height {newBlockHeight} and received a reward of {totalReward} {this.network.CoinTicker}.");

        }

        StakingCoin[] GetOutputs()
        {
            try
            {
                this.walletManager.WalletSemaphore.Wait();
                var coins = this.walletManager.GetBudget(out var balance, true);
                this.logger.LogInformation($"Fetched {coins.Length} utxos, value for staking is {balance.SpendableAmount}.");
                return coins;
            }
            finally
            {
                this.walletManager.WalletSemaphore.Release();
            }

        }

        static long GetCurrentBlockTime()
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long blockTime = currentTime - currentTime % 64;
            return blockTime;
        }
    }


}
