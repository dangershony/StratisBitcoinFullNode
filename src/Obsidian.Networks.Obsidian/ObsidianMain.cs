using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Features.Consensus.Rules.CommonRules;
using Stratis.Bitcoin.Features.Consensus.Rules.ProvenHeaderRules;
using Stratis.Bitcoin.Features.MemoryPool.Rules;

namespace Obsidian.Networks.Obsidian
{
    public class ObsidianMain : Network
    {
	    const string ObsidianRootFolderName = "obsidian";
	    const string ObsidianDefaultConfigFilename = "obsidian.conf";
	    const string NetworkName = "ObsidianMain";
	    const string Ticker = "ODN";

		public ObsidianMain()
        {
			// The message start string is designed to be unlikely to occur in normal data.
			// The characters are rarely used upper ASCII, not valid as UTF-8, and produce
			// a large 4-byte int at any alignment.
			var messageStart = new byte[4];
	        messageStart[0] = 0x4f; // ODN
	        messageStart[1] = 0x64; // ODN
			messageStart[2] = 0x6e; // ODN
			messageStart[3] = 0x31; // ODN
			uint magic = BitConverter.ToUInt32(messageStart, 0);

            this.Name = NetworkName;
			this.Magic = magic;
            this.DefaultPort = 56660; // ODN
	        this.DefaultMaxOutboundConnections = 16;
	        this.DefaultMaxInboundConnections = 109;
			this.DefaultRPCPort = 56661; // ODN
	        this.DefaultAPIPort = 37221;
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
	        this.GenesisTime = 1503532800;  // ODN
	        this.GenesisNonce = 36151509;  // ODN
	        this.GenesisBits = new Target(new uint256("000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")).ToCompact(); // ODN, note the five zeros
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
		        maxStandardTxSigopsCost: 20_000 / 5, 
                witnessScaleFactor: 4
	        );

			this.Consensus = new NBitcoin.Consensus(
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
				bip9Deployments: new NoBIP9Deployments(), // ODN: no BIP9Deployments
				bip34Hash: new uint256("0x000000000000024b89b42a942fe0d9fea3bb44ab7bd1b19115dd6a759c0808b8"), // ??
				ruleChangeActivationThreshold: 1916, // 95% of 2016
				minerConfirmationWindow: 2016, // nPowTargetTimespan / nPowTargetSpacing
				maxReorgLength: 500,
				// defaultAssumeValid: new uint256("0x15a792c680bf348b2a73be99adaf6cd9890be4f1a3895a800f212a43c0232c8b"),  // ODN: Block 32100 hash
				defaultAssumeValid: uint256.Zero,  // ODN: verify all for now!
				maxMoney: long.MaxValue,
				coinbaseMaturity: 50,
				premineHeight: 2,
				premineReward: Money.Coins(98000000), // ODN
				proofOfWorkReward: Money.Coins(4), // ODN
				powTargetTimespan: TimeSpan.FromSeconds(14 * 24 * 60 * 60), // two weeks
				powTargetSpacing: TimeSpan.FromSeconds(10 * 60),
				powAllowMinDifficultyBlocks: false,
				posNoRetargeting: false,
				powNoRetargeting: false,
				powLimit: new Target(new uint256("000000ffffffffffffffffffffffffffffffffffffffffffffffffffffffffff")), // ODN, note the five zeros
				minimumChainWork: null,
				isProofOfStake: true,
				lastPowBlock: 12500,
				proofOfStakeLimit: new BigInteger(uint256.Parse("00000fffffffffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
				proofOfStakeLimitV2: new BigInteger(uint256.Parse("000000000000ffffffffffffffffffffffffffffffffffffffffffffffffffff").ToBytes(false)),
				proofOfStakeReward: Money.Coins(20) // ODN
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
				{ 0, new CheckpointInfo(new uint256("0x0000006dd8a92f58e952fa61c9402b74a381a69d1930fb5cc12c73273fab5f0a"), new uint256("0x0000000000000000000000000000000000000000000000000000000000000000")) }
				// TODO: copy checkpoints from Obsidian-Qt
            };

			this.Bech32Encoders = new Bech32Encoder[2];
	        // Bech32 is currently unsupported - once supported uncomment lines below
	        //var encoder = new Bech32Encoder("bc");
	        //this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = encoder;
	        //this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = encoder;
	        this.Bech32Encoders[(int)Bech32Type.WITNESS_PUBKEY_ADDRESS] = null;
	        this.Bech32Encoders[(int)Bech32Type.WITNESS_SCRIPT_ADDRESS] = null;

			// No blocks will ever be pulled from seed nodes. They are just for address propagation.
			// Do not put gateway nodes in this list.
			this.DNSSeeds = new List<DNSSeedData>
            {
	            // The Obsidian DNS seeds are also added as fixedSeeds below - investigate whether that's a good or bad idea
	            new DNSSeedData("obsidianblockchain1.westeurope.cloudapp.azure.com", "obsidianblockchain1.westeurope.cloudapp.azure.com"),
	            new DNSSeedData("obsidianblockchain2.westeurope.cloudapp.azure.com", "obsidianblockchain2.westeurope.cloudapp.azure.com")
			};

			// No blocks will ever be pulled from seed nodes. They are just for address propagation.
	        // Do not put gateway nodes in this list.
			string[] seedNodes =
            {
	            "104.45.21.229", "23.101.75.57",	// IP addresses of the Obsidian (c++) IP seed nodes
            }; 
			this.SeedNodes = this.ConvertToNetworkAddresses(seedNodes, this.DefaultPort).ToList();

            this.StandardScriptsRegistry = new ObsidianStandardScriptsRegistry();  // Is this needed for Obsidian?

			Assert(this.Consensus.HashGenesisBlock == uint256.Parse("0x0000006dd8a92f58e952fa61c9402b74a381a69d1930fb5cc12c73273fab5f0a")); // ODN
	        Assert(this.Genesis.Header.HashMerkleRoot == uint256.Parse("0x062e0ef40ca83213f645710bf497cc68220d42ac2254d31bbc8fb64a4d207209")); // ODN


            this.RegisterRules(this.Consensus);
            this.RegisterMempoolRules(this.Consensus);
        }

        protected void RegisterRules(IConsensus consensus)
        {
            consensus.ConsensusRules
                .Register<HeaderTimeChecksRule>()
                .Register<HeaderTimeChecksPosRule>()
                .Register<StratisBugFixPosFutureDriftRule>()
                .Register<CheckDifficultyPosRule>()
                .Register<StratisHeaderVersionRule>()
                .Register<ProvenHeaderSizeRule>()
                .Register<ProvenHeaderCoinstakeRule>();

            consensus.ConsensusRules
                .Register<BlockMerkleRootRule>()
                .Register<PosBlockSignatureRepresentationRule>()
                .Register<PosBlockSignatureRule>();

            consensus.ConsensusRules
                .Register<SetActivationDeploymentsPartialValidationRule>()
                .Register<PosTimeMaskRule>()

                // rules that are inside the method ContextualCheckBlock
                .Register<TransactionLocktimeActivationRule>()
                .Register<CoinbaseHeightActivationRule>()
                .Register<WitnessCommitmentsRule>()
                .Register<BlockSizeRule>()

                // rules that are inside the method CheckBlock
                .Register<EnsureCoinbaseRule>()
                .Register<CheckPowTransactionRule>()
                .Register<CheckPosTransactionRule>()
                .Register<CheckSigOpsRule>()
                .Register<PosCoinstakeRule>();

            consensus.ConsensusRules
                .Register<SetActivationDeploymentsFullValidationRule>()

                .Register<CheckDifficultyHybridRule>()

                // rules that require the store to be loaded (coinview)
                .Register<LoadCoinviewRule>()
                .Register<TransactionDuplicationActivationRule>()
                .Register<PosCoinviewRule>() // implements BIP68, MaxSigOps and BlockReward calculation
                                             // Place the PosColdStakingRule after the PosCoinviewRule to ensure that all input scripts have been evaluated
                                             // and that the "IsColdCoinStake" flag would have been set by the OP_CHECKCOLDSTAKEVERIFY opcode if applicable.
                .Register<PosColdStakingRule>()
                .Register<SaveCoinviewRule>();
        }

        protected void RegisterMempoolRules(IConsensus consensus)
        {
            consensus.MempoolRules = new List<Type>()
            {
                typeof(CheckConflictsMempoolRule),
                typeof(CheckCoinViewMempoolRule),
                typeof(CreateMempoolEntryMempoolRule),
                typeof(CheckSigOpsMempoolRule),
                typeof(CheckFeeMempoolRule),
                typeof(CheckRateLimitMempoolRule),
                typeof(CheckAncestorsMempoolRule),
                typeof(CheckReplacementMempoolRule),
                typeof(CheckAllInputsMempoolRule)
            };
        }

        // new method, using ConsensusFactory
        static Block CreateObsidianGenesisBlock(ConsensusFactory consensusFactory, uint nTime, uint nNonce, uint nBits, int nVersion, Money genesisReward)
	    {
			string pszTimestamp = "https://en.wikipedia.org/w/index.php?title=Brave_New_World&id=796766418";

			Transaction txNew = consensusFactory.CreateTransaction();
		    txNew.Version = 1;
		    txNew.Time = nTime;
		    txNew.AddInput(new TxIn()
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
