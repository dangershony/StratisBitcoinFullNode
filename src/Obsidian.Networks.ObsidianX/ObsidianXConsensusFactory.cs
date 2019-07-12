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
                if (genesis.GetHash() != uint256.Parse("0x00000604e84044bf137dba3f4aa871440b5b438c8fa7c9eea50964a9926ed420") ||
                    genesis.Header.HashMerkleRoot != uint256.Parse("0x11c00cb2d53add0ba7df062823e64b1fb1676194f7bbcada58b0873463c1ebef"))
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
