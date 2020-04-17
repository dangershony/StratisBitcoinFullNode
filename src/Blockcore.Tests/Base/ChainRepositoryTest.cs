﻿using System;
using System.Collections.Generic;
using Blockcore.Base;
using Blockcore.Tests.Common;
using Blockcore.Utilities;
using DBreeze;
using DBreeze.DataTypes;
using LevelDB;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Xunit;

namespace Blockcore.Tests.Base
{
    public class ChainRepositoryTest : TestBase
    {
        private readonly DataStoreSerializer dataStoreSerializer;

        public ChainRepositoryTest() : base(KnownNetworks.StratisRegTest)
        {
            this.dataStoreSerializer = new DataStoreSerializer(this.Network.Consensus.ConsensusFactory);
        }

        [Fact]
        public void SaveWritesChainToDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ChainIndexer(KnownNetworks.StratisRegTest);
            this.AppendBlock(chain);

            using (var repo = new ChainRepository(dir, new LoggerFactory(), new MemoryHeaderStore(), chain.Network))
            {
                repo.SaveAsync(chain).GetAwaiter().GetResult();
            }

            using (var engine = new DB(new Options { CreateIfMissing = true }, dir))
            {
                ChainedHeader tip = null;
                var itr = engine.GetEnumerator();

                while (itr.MoveNext())
                {
                    var data = new ChainRepository.ChainRepositoryData();
                    data.FromBytes(itr.Current.Value, this.Network.Consensus.ConsensusFactory);

                    tip = new ChainedHeader(data.Hash, data.Work, tip);
                    if (tip.Height == 0) tip.SetBlockHeaderStore(new MemoryHeaderStore());
                }
                Assert.Equal(tip, chain.Tip);
            }
        }

        [Fact]
        public void GetChainReturnsConcurrentChainFromDisk()
        {
            string dir = CreateTestDir(this);
            var chain = new ChainIndexer(KnownNetworks.StratisRegTest);
            ChainedHeader tip = this.AppendBlock(chain);

            using (var engine = new DB(new Options { CreateIfMissing = true }, dir))
            {
                using (var batch = new WriteBatch())
                {
                    ChainedHeader toSave = tip;
                    var blocks = new List<ChainedHeader>();
                    while (toSave != null)
                    {
                        blocks.Insert(0, toSave);
                        toSave = toSave.Previous;
                    }

                    foreach (ChainedHeader block in blocks)
                    {
                        batch.Put(BitConverter.GetBytes(block.Height),
                            new ChainRepository.ChainRepositoryData()
                            { Hash = block.HashBlock, Work = block.ChainWorkBytes }
                                .ToBytes(this.Network.Consensus.ConsensusFactory));
                    }

                    engine.Write(batch);
                }
            }
            using (var repo = new ChainRepository(dir, new LoggerFactory(), new MemoryHeaderStore(), chain.Network))
            {
                var testChain = new ChainIndexer(KnownNetworks.StratisRegTest);
                testChain.SetTip(repo.LoadAsync(testChain.Genesis).GetAwaiter().GetResult());
                Assert.Equal(tip, testChain.Tip);
            }
        }

        public ChainedHeader AppendBlock(ChainedHeader previous, params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader last = null;
            uint nonce = RandomUtils.GetUInt32();
            foreach (ChainIndexer chain in chainsIndexer)
            {
                Block block = this.Network.Consensus.ConsensusFactory.CreateBlock();
                block.AddTransaction(this.Network.CreateTransaction());
                block.UpdateMerkleRoot();
                block.Header.HashPrevBlock = previous == null ? chain.Tip.HashBlock : previous.HashBlock;
                block.Header.Nonce = nonce;
                if (!chain.TrySetTip(block.Header, out last))
                    throw new InvalidOperationException("Previous not existing");
            }
            return last;
        }

        private ChainedHeader AppendBlock(params ChainIndexer[] chainsIndexer)
        {
            ChainedHeader index = null;
            return this.AppendBlock(index, chainsIndexer);
        }
    }
}