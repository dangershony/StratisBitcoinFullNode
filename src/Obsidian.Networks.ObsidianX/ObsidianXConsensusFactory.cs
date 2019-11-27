using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Obsidian.Networks.ObsidianX
{
    public class ObsidianXConsensusFactory : PosConsensusFactory
    {
        public override BlockHeader CreateBlockHeader()
        {
            return new ObsidianXBlockHeader();
        }

        public override ProvenBlockHeader CreateProvenBlockHeader()
        {
            return new ObsidianXProvenBlockHeader();
        }

        public override ProvenBlockHeader CreateProvenBlockHeader(PosBlock block)
        {
            var provenBlockHeader = new ObsidianXProvenBlockHeader(block);

            // Serialize the size.
            provenBlockHeader.ToBytes(this);

            return provenBlockHeader;
        }

        public override Transaction CreateTransaction()
        {
            return new Transaction();
        }

        public override Transaction CreateTransaction(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var transaction = new Transaction();
            transaction.ReadWrite(bytes, this);
            return transaction;
        }

        public override Transaction CreateTransaction(string hex)
        {
            if (hex == null)
                throw new ArgumentNullException(nameof(hex));

            return CreateTransaction(Encoders.Hex.DecodeData(hex));
        }

        public override Block CreateBlock()
        {
            return base.CreateBlock(); // TODO: override this as well?
        }

        public override T TryCreateNew<T>()
        {
            return base.TryCreateNew<T>(); // TODO: override this as well?
        }

        public override ProtocolCapabilities GetProtocolCapabilities(uint protocolVersion)
        {
            return base.GetProtocolCapabilities(protocolVersion); // TODO: override this as well?
        }


        public Block CreateObsidianGenesisBlock(uint genesisTime, uint genesisNonce, uint genesisBits, int genesisVersion, Money genesisReward, bool? mine = false)
        {
            if (mine == true)
                MineGenesisBlock(genesisTime, genesisBits, genesisVersion, genesisReward);

            string pszTimestamp = "This is Obsidian!";

            Transaction txNew = CreateTransaction();
            Debug.Assert(txNew.GetType() == typeof(Transaction));

            txNew.Version = 1;
            //if(txNew is PosTransaction posTransaction)
            //    posTransaction.Time = genesisTime;
            txNew.AddInput(new TxIn()
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoding.UTF8.GetBytes(pszTimestamp)))
            });
            txNew.AddOutput(new TxOut()
            {
                Value = genesisReward,
            });
            Block genesis = CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(genesisTime);
            genesis.Header.Bits = genesisBits;
            genesis.Header.Nonce = genesisNonce;
            genesis.Header.Version = genesisVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();

            if (mine == false)
                if (genesis.GetHash() != uint256.Parse("00000a6ac74dce2fee731140408b49178a96b76e21818528bf1113b18ba516f2") ||
                    genesis.Header.HashMerkleRoot != uint256.Parse("795951e236afb8bee690c51bd6673b84ae3d462806c2d1f15d6b52e40c1f5ae7"))
                    throw new InvalidOperationException("Invalid network");
            return genesis;
        }

        void MineGenesisBlock(uint genesisTime, uint genesisBits, int genesisVersion, Money genesisReward)
        {
            Parallel.ForEach(new long[] { 0, 1, 2, 3, 4, 5, 6, 7 }, l =>
            {
                if (Utils.UnixTimeToDateTime(genesisTime) > DateTime.UtcNow)
                    throw new Exception("Time must not be in the future");
                uint nonce = 0;
                while (!CreateObsidianGenesisBlock(genesisTime, nonce, genesisBits, genesisVersion, genesisReward, null).GetHash().ToString().StartsWith("00000"))
                    nonce += 8;
                var genesisBlock = CreateObsidianGenesisBlock(genesisTime, nonce, genesisBits, genesisVersion, genesisReward, null);
                throw new Exception($"Found: Nonce:{nonce}, Hash: {genesisBlock.GetHash()}, Hash Merkle Root: {genesisBlock.Header.HashMerkleRoot}");
            });
        }
    }
}