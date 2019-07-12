﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;

namespace Obsidian.Networks.ObsidianX
{
    public class ObsidianXMain : Network
    {
        public ObsidianXMain()
        {
            this.Name = "ObsidianXMain";
            this.CoinTicker = "ODX";

            this.RootFolderName = "obsidianx";
            this.DefaultConfigFilename = "obsidianx.conf";

            this.Magic = BitConverter.ToUInt32(Encoding.ASCII.GetBytes("ODX1"), 0);
            this.DefaultPort = 46660;
            this.DefaultRPCPort = 46661;
            this.DefaultAPIPort = 37221;
            this.DefaultMaxOutboundConnections = 16;
            this.DefaultMaxInboundConnections = 109;
            this.MaxTimeOffsetSeconds = 25 * 60;

            this.MaxTipAge = Convert.ToInt32(TimeSpan.FromDays(30).TotalSeconds);  // Set to 7 days for development to fix IBD, Standard value: 2 * 60 * 60s (120 minutes)
            this.MinTxFee = 100;
            this.FallbackFee = 500;
            this.MinRelayTxFee = 100;


            var consensusFactory = new ObsidianXConsensusFactory();
            this.GenesisTime = Utils.DateTimeToUnixTime(new DateTime(2019, 7, 12, 23, 12, 23, DateTimeKind.Utc));
            this.GenesisNonce = 11056784;
            this.GenesisBits = new Target(new uint256("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"));
            this.GenesisVersion = 1;
            this.GenesisReward = Money.Zero;
            this.Genesis = consensusFactory.CreateObsidianGenesisBlock(this.GenesisTime, this.GenesisNonce, this.GenesisBits, this.GenesisVersion, this.GenesisReward);


            var consensusOptions = new ObsidianXPoSConsensusOptions(
                maxBlockBaseSize: 1_000_000,
                maxStandardVersion: 2,
                maxStandardTxWeight: 100_000,
                maxBlockSigopsCost: 20_000,
                maxStandardTxSigopsCost: 20_000 / 5,
                witnessScaleFactor: 4
            );

            var bip9Deployments = new ObsidianXBIP9DeploymentsArray
            {
                [ObsidianXBIP9DeploymentsArray.TestDummy] = new BIP9DeploymentsParameters(28, BIP9DeploymentsParameters.AlwaysActive, 999999999),    // BTC main uses bit 28 for CSV
                [ObsidianXBIP9DeploymentsArray.ColdStaking] = new BIP9DeploymentsParameters(27, BIP9DeploymentsParameters.AlwaysActive, 999999999),  // use a high bit for ColdStaking
                [ObsidianXBIP9DeploymentsArray.CSV] = new BIP9DeploymentsParameters(0, BIP9DeploymentsParameters.AlwaysActive, 999999999),           // BTC main uses bit 0 for CSV
                [ObsidianXBIP9DeploymentsArray.Segwit] = new BIP9DeploymentsParameters(1, BIP9DeploymentsParameters.AlwaysActive, 999999999)         // BTC main uses bit 1 for SegWit
            };


            this.Consensus = new Consensus(
                consensusFactory: consensusFactory,
                consensusOptions: consensusOptions,
                coinType: 442,  // ObsidianXMain: 442 (hex: 1ba) Coin Type for BIP-0044, see https://github.com/satoshilabs/slips/blob/master/slip-0044.md
                hashGenesisBlock: this.Genesis.GetHash(),
                subsidyHalvingInterval: 210000,
                majorityEnforceBlockUpgrade: 750,
                majorityRejectBlockOutdated: 950,
                majorityWindow: 1000,
                buriedDeployments: new BuriedDeploymentsArray
                {
                    [BuriedDeployments.BIP34] = 0,  // Block v2, Height in Coinbase
                    [BuriedDeployments.BIP65] = 0,  // Opcode OP_CHECKLOCKTIMEVERIFY
                    [BuriedDeployments.BIP66] = 0   // Strict DER signatures
                },
                bip9Deployments: bip9Deployments,
                bip34Hash: this.Genesis.GetHash(), // always active
                ruleChangeActivationThreshold: 1916, // 95% of 2016
                minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
                maxReorgLength: 500,
                // defaultAssumeValid: new uint256("0x15a792c680bf348b2a73be99adaf6cd9890be4f1a3895a800f212a43c0232c8b"),  
                defaultAssumeValid: uint256.Zero,  // verify all for now!
                maxMoney: long.MaxValue,
                coinbaseMaturity: 50,
                premineHeight: 2,
                premineReward: Money.Coins(110000000), 
                proofOfWorkReward: Money.Coins(10),
                powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
                powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
                powAllowMinDifficultyBlocks: false,
                posNoRetargeting: false,
                powNoRetargeting: false,
                powLimit: new Target(this.GenesisBits),
                minimumChainWork: null,
                isProofOfStake: true,
                lastPowBlock: 50000,
                proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
                proofOfStakeReward: Money.Coins(15) 
                );

            this.Consensus.PosEmptyCoinbase = false;
            this.StandardScriptsRegistry = new ObsidianXStandardScriptsRegistry();

            this.Base58Prefixes = new byte[0][];
           
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

           
        }
    }
}
