using System;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Interfaces;

namespace Obsidian.Features.SegWitWallet.Tests
{
    public static class StaticWallet
    {
        public static readonly OpcodeType WitnessVersion = OpcodeType.OP_0;

        public static Key PrivateKey1;

        

        public static Key PrivateKey2;

        public static PubKey CPubKey1;
        public static PubKey CPubKey2;

        public static BitcoinWitPubKeyAddress PWPKHAddress1;
        public static BitcoinWitPubKeyAddress PWPKHAddress2;

        public static Script PWPKH1Script;
        public static Script PWPKH2Script;
        public static string Bech32Adr1;

        static Network _network;
        static FullNode _fullNode;

        public static byte[] Key1Bytes;
        public static byte[] Key2Bytes;





        static void Fill(byte pattern, byte[] bytes)
        {
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = pattern;
        }

        public static void CreateWallet(Network network, FullNode fullNode)
        {
            _network = network;
            _fullNode = fullNode;

            Key1Bytes = new byte[32];
            Fill(23, Key1Bytes);
            PrivateKey1 = new Key(Key1Bytes);
            CPubKey1 = PrivateKey1.PubKey.Compress();

            Key2Bytes = new byte[32];
            Fill(33, Key2Bytes);
            PrivateKey2 = new Key(Key2Bytes);
            CPubKey2 = PrivateKey2.PubKey.Compress();


            WitKeyId hash160Cpk1 = CPubKey1.WitHash;
            PWPKHAddress1 = new BitcoinWitPubKeyAddress(hash160Cpk1, _network);



            WitKeyId hash160Cpk2 = CPubKey2.WitHash;
            PWPKHAddress2 = new BitcoinWitPubKeyAddress(hash160Cpk2, _network);

            PWPKH1Script = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160Cpk1.ToBytes()));
            PWPKH2Script = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160Cpk2.ToBytes()));

            Bech32Encoder encoder = network.GetBech32Encoder(Bech32Type.WITNESS_PUBKEY_ADDRESS, true);
            Bech32Adr1 = encoder.Encode((byte)WitnessVersion, PWPKH1Script.ToBytes());
        }

        static Spendable GetTransactionWithSpendableOutputs()
        {
            int blockHeight = 1;

            var searchFor = PWPKH1Script.ToString();

            while (true)
            {
                var chainedHeader = _fullNode.ChainIndexer.GetHeader(blockHeight);
                var block = _fullNode.BlockStore().GetBlock(chainedHeader.HashBlock);
                if (block == null)
                    return null;
                foreach (var tx in block.Transactions)
                {
                    foreach (var o in tx.Outputs)
                    {
                        var outIndex = 0;
                        if (o.ScriptPubKey.ToString() == searchFor)
                        {
                            return new Spendable { Transaction = tx, TxOut = o, OutIndex = outIndex };
                        }

                        outIndex++;
                    }
                }

                blockHeight++;
            }

        }

        public static Transaction CreateTx()
        {
            var spendable = GetTransactionWithSpendableOutputs();
            if (spendable == null)
                return null;
            var tx = new Transaction();
            tx.Version = 1;
            tx.Inputs.Add(new TxIn(new OutPoint(spendable.Transaction,spendable.OutIndex)));
            tx.Outputs.Add(new TxOut(Money.COIN, StaticWallet.PWPKH2Script));
            tx.Outputs.Add(new TxOut(Money.Coins(9) - Money.FromUnit(1000, MoneyUnit.Satoshi), PWPKH1Script));

            tx.Sign(_network, PrivateKey1, new Coin(spendable.Transaction, (uint) spendable.OutIndex));
            return tx;

        }

        internal static async void SendTx(Transaction tx)
        {
            try
            {
                await _fullNode.BroadcasterManager().BroadcastTransactionAsync(tx);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not broadcast transaction: {e.Message}");
            }
            
        }

        public static IBroadcasterManager BroadcasterManager(this FullNode fullNode)
        {
            return fullNode.NodeService<IBroadcasterManager>();
        }

        public static IBlockStore BlockStore(this FullNode fullNode)
        {
            return fullNode.NodeService<IBlockStore>();
        }

        public static void PrintBlocks()
        {
            int blockHeight = 1;
            while (true)
            {
                var chainedHeader = _fullNode.ChainIndexer.GetHeader(blockHeight);
                if (chainedHeader == null)
                    return;
                var block = _fullNode.BlockStore().GetBlock(chainedHeader.HashBlock);
                if (block == null)
                    return;
                Console.WriteLine($"{chainedHeader.Height}: {block.Header.GetHash()}  {block.Transactions.Count}");

                blockHeight++;
            }
        }
    }
}