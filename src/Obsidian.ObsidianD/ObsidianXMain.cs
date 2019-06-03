using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;

namespace Obsidian.ObsidianD
{
    public class ObsidianXMain : Network
    {
        const string ObsidianRootFolderName = "obsidianx";  // obsidianx
        const string ObsidianDefaultConfigFilename = "obsidianx.conf";  // obsidianx
        const string NetworkName = "ObsidianXMain";
        const string Ticker = "ODX";

        public ObsidianXMain()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x4f; // ODN
            messageStart[1] = 0x64; // ODN
            messageStart[2] = 0x6e; // ODN
            messageStart[3] = 0x32; // ODN uses 0x31 here, we now use 0x32 for TODX
            uint magic = BitConverter.ToUInt32(messageStart, 0);

            this.Name = NetworkName;
            this.Magic = magic;
            this.DefaultPort = 46660; // ODN uses 56660, we now use 46660 for TODX
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 46661; // ODN uses 56661, we now use 46661 for TODX
            this.DefaultAPIPort = 37221; // ODN uses 37221, we now use 47221 for TODX
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 60000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = ObsidianRootFolderName;
            this.DefaultConfigFilename = ObsidianDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = Ticker;

            var consensusFactory = new ObsidianConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = Utils.DateTimeToUnixTime(new DateTime(2019, 6, 2, 15, 23, 23, DateTimeKind.Utc));  // ODN
            this.GenesisNonce = 2891582;  // ODX
            this.GenesisBits = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")).ToCompact(); // ODN, note the five zeros
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            this.Genesis = CreateObsidianGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            // Taken from StratisX.
            // TODO: Check if this is compatible with ObsidianQt
            var consensusOptions = new ObsidianPoSConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5
            );

            var bip9Deployments = new ObsidianBIP9DeploymentsArray
            {
                [ObsidianBIP9DeploymentsArray.TestDummy] = new BIP9DeploymentsParameters(28, Utils.DateTimeToUnixTime(new DateTime(2018, 1, 1)), 999999999),
                [ObsidianBIP9DeploymentsArray.CSV] = new BIP9DeploymentsParameters(0, Utils.DateTimeToUnixTime(new DateTime(2018, 1, 1)), 999999999),
                [ObsidianBIP9DeploymentsArray.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999)
            };


            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 105,
                hashGenesisBlock: this.Genesis.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: new BuriedDeploymentsArray
                {
                    [BuriedDeployments.BIP34] = 0, // ODN: this was set before to: 227931
                    [BuriedDeployments.BIP65] = 0, // ODN: this was set before to: 388381
                    [BuriedDeployments.BIP66] = 0 // ODN: this was set before to: 363725
                },
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), // ??
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                // defaultAssumeValid: new uint256("0x15a792c680bf348b2a73be99adaf6cd9890be4f1a3895a800f212a43c0232c8b"),  // ODN: Block 32100 hash
                defaultAssumeValid: uint256.Zero,  // ODN: verify all for now!
                maxMoney: long.MaxValue,
                coinbaseMaturity: 50,
                premineHeight: 2,
                premineReward: Money.Coins(110000000), // ODN
                proofOfWorkReward: Money.Coins(10), // ODN
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), // ODN, note the five zeros
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 50000,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(15) // ODN
                );


            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (75) }; // ODN
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (125) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (75 + 128) };  // ODN
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };

            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xC2), (0x1E) }; // matches Obsidian-Qt, StratisX (but not Stratis C# (it's unused though)
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0xDD) }; // matches Obsidian-Qt, StratisX (but not Stratis C# (it's unused though)

            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            // TODO: add the other Obsidian checkpoints
            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                // { 0, new CheckpointInfo(new uint256("0x0000006dd8a92f58e952fa61c9402b74a381a69d1930fb5cc12c73273fab5f0a"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) }
                // TODO: copy checkpoints from Obsidian-Qt
            };

            this.Bech32Encoders = new Bech32Encoder[2];
            var encoder = new Bech32Encoder("odx");
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;

            // No blocks will ever be pulled from seed nodes. They are just for address propagation.
            // Do not put gateway nodes in this list.
            this.DNSSeeds = new List<DNSSeedData>
            {
                // The Obsidian DNS seeds are also added as fixedSeeds below - investigate whether that's a good or bad idea
                // new DNSSeedData("obsidianblockchain1.westeurope.cloudapp.azure.com", "obsidianblockchain1.westeurope.cloudapp.azure.com"),
                //new DNSSeedData("obsidianblockchain2.westeurope.cloudapp.azure.com", "obsidianblockchain2.westeurope.cloudapp.azure.com")
            };

            // No blocks will ever be pulled from seed nodes. They are just for address propagation.
            // Do not put gateway nodes in this list.
            string[] seedNodes =
            {
	            //"104.45.21.229", "23.101.75.57",	// IP addresses of the Obsidian (c++) IP seed nodes
            };
            this.SeedNodes = this.ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            this.StandardScriptsRegistry = new ObsidianStandardScriptsRegistry();  // With this class, a copy of the StratisStandardScriptsRegistry, we need not reference Stratis.Bitcoin.Networks

            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x00000ad8ed1fc239b47507c55246ad598b6efee0b1618aac43c9728bc3dc850a")); // ODX
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x83daf4f90e51f4d8b45f1076ba18250fc7b2568856bb50296adbfed005545a10")); // ODX
        }


        static void Find()
        {

        }


        // new method, using ConsensusFactory
        static Block CreateObsidianGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            string pszTimestamp = "这是黑曜石";  // "This is Obsidian" (Chinese)

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.Time = nTime;
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
            Block genesis = consensusFactory.CreateBlock();
            genesis.Header.BlockTime = Utils.UnixTimeToDateTime(nTime);
            genesis.Header.Bits = nBits;
            genesis.Header.Nonce = nNonce;
            genesis.Header.Version = nVersion;
            genesis.Transactions.Add(txNew);
            genesis.Header.HashPrevBlock = uint256.Zero;
            genesis.UpdateMerkleRoot();
            return genesis;
        }


    }
}
