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

            string pszTimestamp = "这是黑曜石";  // "This is Obsidian" (Chinese)

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
                if (genesis.GetHash() != uint256.Parse("0x00000aac21cdf38f4adad8ffe497c120055d313c6dbffb1172cff0078fb40d47") ||
                    genesis.Header.HashMerkleRoot != uint256.Parse("0x80ca98cb11b36e1db2d34755bead7852a85661a4411259a26ec498b24a96b011"))
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
