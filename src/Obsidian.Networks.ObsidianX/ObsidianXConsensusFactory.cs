using System;
using System.Text;
using System.Threading.Tasks;
using NBitcoin;

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

        public Block CreateObsidianGenesisBlock(uint genesisTime, uint genesisNonce, uint genesisBits, int genesisVersion, Money genesisReward, bool? mine = false)
        {
            if (mine == true)
                MineGenesisBlock(genesisTime, genesisBits, genesisVersion, genesisReward);

            string pszTimestamp = "这是黑曜石和危险的古海神";  // "This is Obsidian" (Chinese)

            Transaction txNew = CreateTransaction();
            txNew.Version = 1;
            txNew.Time = genesisTime;
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
                if (genesis.GetHash() != uint256.Parse("0000035b9c5c801af1bd828347d8160ed0237a56dd105967a77212f0c8e70bae") ||
                    genesis.Header.HashMerkleRoot != uint256.Parse("b5a77bfde3295b5875560c85ea8099e365ddf64639714a7e5f61574567a6ae39"))
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
