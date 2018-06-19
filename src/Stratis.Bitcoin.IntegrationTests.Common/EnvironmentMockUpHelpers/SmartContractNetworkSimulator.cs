﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet;

namespace Stratis.Bitcoin.IntegrationTests.Common.EnvironmentMockUpHelpers
{
    public class SmartContractNetworkSimulator : IDisposable
    {
        public List<CoreNode> Nodes { get; private set; }

        private readonly NodeBuilder nodeBuilder;

        public SmartContractNetworkSimulator([CallerMemberName] string caller = null)
        {
            this.nodeBuilder = NodeBuilder.Create(caller: caller);
        }

        public void Initialize(int nodesCount)
        {
            this.Nodes = new List<CoreNode>();

            //create nodes
            for (int i = 0; i < nodesCount; ++i)
            {
                CoreNode node = this.nodeBuilder.CreateSmartContractNode(true);
                node.NotInIBD();

                node.FullNode.WalletManager().CreateWallet("dummyPassword", "dummyWallet");
                var address = node.FullNode.WalletManager().GetUnusedAddress(new WalletAccountReference("dummyWallet", "account 0"));
                var wallet = node.FullNode.WalletManager().GetWalletByName("dummyWallet");
                var key = wallet.GetExtendedPrivateKeyForAddress("dummyPassword", address).PrivateKey;
                node.SetDummyMinerSecret(new BitcoinSecret(key, node.FullNode.Network));

                this.Nodes.Add(node);
            }
        }

        public bool AreAllNodesAtSameHeight()
        {
            return this.Nodes.Select(i => i.FullNode.Chain.Height).Distinct().Count() == 1;
        }

        public void Dispose()
        {
            this.nodeBuilder.Dispose();
        }

        public bool DidAllNodesReachHeight(int height)
        {
            return this.Nodes.All(i => i.FullNode.Chain.Height >= height);
        }

        public void MakeSureEachNodeCanMineAndSync()
        {
            foreach (var node in this.Nodes)
            {
                Thread.Sleep(1000);
                var currentHeight = node.FullNode.Chain.Height;

                node.GenerateStratisWithMiner(1);
                TestHelper.WaitLoop(() => TestHelper.IsNodeSynced(node));
                TestHelper.WaitLoop(new Func<bool>(delegate { return node.FullNode.Chain.Height == currentHeight + 1; }));
                TestHelper.WaitLoop(new Func<bool>(delegate { return AreAllNodesAtSameHeight(); }));
            }
        }
    }
}