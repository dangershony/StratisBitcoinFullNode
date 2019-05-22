using System;
using System.Collections.Generic;
using System.Net;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using NBitcoin.Protocol;
using Stratis.Bitcoin.Networks.Deployments;
using Stratis.Bitcoin.Networks.Policies;

namespace Stratis.Bitcoin.Networks
{
    public class SolarisMain : Network
    {
        /// <summary> Solaris maximal value for the calculated time offset. If the value is over this limit, the time syncing feature will be switched off. </summary>
        public const int SolarisMaxTimeOffsetSeconds = 25 * 60;

        /// <summary> Solaris default value for the maximum tip age in seconds to consider the node in initial block download (2 hours). </summary>
        public const int SolarisDefaultMaxTipAgeInSeconds = 2 * 60 * 60;

        /// <summary> The name of the root folder containing the different Solaris blockchains (SolarisMain, SolarisTest, SolarisRegTest). </summary>
        public const string SolarisRootFolderName = "solaris";

        /// <summary> The default name used for the Solaris configuration file. </summary>
        public const string SolarisDefaultConfigFilename = "solaris.conf";

        public SolarisMain()
        {
            // The message start string is designed to be unlikely to occur in normal data.
            // The characters are rarely used upper ASCII, not valid as UTF-8, and produce
            // a large 4-byte int at any alignment.
            var messageStart = new byte[4];
            messageStart[0] = 0x71;
            messageStart[1] = 0x36;
            messageStart[2] = 0x23;
            messageStart[3] = 0x06;
            uint magic = BitConverter.ToUInt32(messageStart, 0); //0x5223570;

            this.Name = "SolarisMain";
            this.NetworkType = NetworkType.Mainnet;
            this.Magic = magic;
            this.DefaultPort = 60000;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.DefaultRPCPort = 61000;
            this.DefaultAPIPort = 62000;
            this.MaxTipAge = 2 * 60 * 60;
            this.MinTxFee = 10000;
            this.FallbackFee = 10000;
            this.MinRelayTxFee = 10000;
            this.RootFolderName = SolarisRootFolderName;
            this.DefaultConfigFilename = SolarisDefaultConfigFilename;
            this.MaxTimeOffsetSeconds = 25 * 60;
            this.CoinTicker = "XLR";

            var consensusFactory = new PosConsensusFactory();

            // Create the genesis block.
            this.GenesisTime = 1470467000;
            this.GenesisNonce = 1831645;
            this.GenesisBits = 0x1e0fffff;
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;

            Block genesisBlock = CreateSolarisGenesisBlock(consensusFactory, this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);

            this.Genesis = genesisBlock;

            // Taken from SolarisX.
            var consensusOptions = new PosConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5
            );

            var buriedDeployments = new BuriedDeploymentsArray
            {
                [BuriedDeployments.BIP34] = 0,
                [BuriedDeployments.BIP65] = 0,
                [BuriedDeployments.BIP66] = 0
            };

            var bip9Deployments = new StratisBIP9Deployments
            {
                [StratisBIP9Deployments.ColdStaking] = new BIP9DeploymentsParameters(2,
                    new DateTime(2018, 12, 1, 0, 0, 0, DateTimeKind.Utc),
                    new DateTime(2019, 12, 1, 0, 0, 0, DateTimeKind.Utc))
            };

            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 105,
                hashGenesisBlock: genesisBlock.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: buriedDeployments,
                bip9Deployments: bip9Deployments,
                bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"),
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                defaultAssumeValid: new uint256("0x50497017e7bb256df205fcbc2caccbe5b516cb33491e1a11737a3bfe83959b9f"), // 1213518
                maxMoney: long.MaxValue,
                coinbaseMaturity: 50,
                premineHeight: 2,
                premineReward: Money.Coins(2500000),
                proofOfWorkReward: Money.Coins(0.25m),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")),
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 12500,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(0.25m)
            );

            this.Base58Prefixes = new byte[12][];
            this.Base58Prefixes[(int)Base58Type.PUBKEY_ADDRESS] = new byte[] { (75) };
            this.Base58Prefixes[(int)Base58Type.SCRIPT_ADDRESS] = new byte[] { (125) };
            this.Base58Prefixes[(int)Base58Type.SECRET_KEY] = new byte[] { (63 + 128) };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_NO_EC] = new byte[] { 0x01, 0x42 };
            this.Base58Prefixes[(int)Base58Type.ENCRYPTED_SECRET_KEY_EC] = new byte[] { 0x01, 0x43 };
            this.Base58Prefixes[(int)Base58Type.EXT_PUBLIC_KEY] = new byte[] { (0x04), (0x88), (0xB2), (0x1E) };
            this.Base58Prefixes[(int)Base58Type.EXT_SECRET_KEY] = new byte[] { (0x04), (0x88), (0xAD), (0xE4) };
            this.Base58Prefixes[(int)Base58Type.PASSPHRASE_CODE] = new byte[] { 0x2C, 0xE9, 0xB3, 0xE1, 0xFF, 0x39, 0xE2 };
            this.Base58Prefixes[(int)Base58Type.CONFIRMATION_CODE] = new byte[] { 0x64, 0x3B, 0xF6, 0xA8, 0x9A };
            this.Base58Prefixes[(int)Base58Type.STEALTH_ADDRESS] = new byte[] { 0x2a };
            this.Base58Prefixes[(int)Base58Type.ASSET_ID] = new byte[] { 23 };
            this.Base58Prefixes[(int)Base58Type.COLORED_ADDRESS] = new byte[] { 0x13 };

            this.Checkpoints = new Dictionary<int, CheckpointInfo>
            {
                //{ 0, new CheckpointInfo(new uint256("0x0000066e91e46e5a264d42c89e1204963b2ee6be230b443e9159020539d972af"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) }
            };

            this.Bech32Encoders = new Bech32Encoder[2];
            this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = null;
            this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = null;

            this.DNSSeeds = new List<DNSSeedData>
            {
                new DNSSeedData("mainnet1.solarisplatform.com", "mainnet1.solarisplatform.com"),
                new DNSSeedData("mainnet2.solarisplatform.com", "mainnet2.solarisplatform.com"),
                new DNSSeedData("mainnet3.solarisplatform.com", "mainnet3.solarisplatform.com"),
                new DNSSeedData("mainnet4.solarisplatform.com", "mainnet4.solarisplatform.com")
            };

            this.SeedNodes = new List<NetworkAddress>
            {
                //TODO Set IP based seed nodes
                //new NetworkAddress(IPAddress.Parse("51.140.231.125"), 16178)
            };

            this.StandardScriptsRegistry = new StratisStandardScriptsRegistry();

            //TODO Mine new genesis block
            Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x"));
            Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x"));
        }

        protected static Block CreateSolarisGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
        {
            const string pszTimestamp = "https://www.solariscoin.com/";

            Transaction txNew = consensusFactory.CreateTransaction();
            txNew.Version = 1;
            txNew.Time = nTime;
            txNew.AddInput(new TxIn
            {
                ScriptSig = new Script(Op.GetPushOp(0), new Op()
                {
                    Code = (OpcodeType)0x1,
                    PushData = new[] { (byte)42 }
                }, Op.GetPushOp(Encoders.ASCII.DecodeData(pszTimestamp)))
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
