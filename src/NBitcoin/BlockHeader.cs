using System;
using NBitcoin.BouncyCastle.Math;
using NBitcoin.Crypto;

namespace NBitcoin
{
    /// <summary>
    /// Nodes collect new transactions into a block, hash them into a hash tree,
    /// and scan through nonce values to make the block's hash satisfy proof-of-work
    /// requirements.  When they solve the proof-of-work, they broadcast the block
    /// to everyone and the block is added to the block chain.  The first transaction
    /// in the block is a special one that creates a new coin owned by the creator
    /// of the block.
    /// </summary>
    public class BlockHeader : IBitcoinSerializable
    {
        public const int Size = 80;

        /// <summary>Current header version.</summary>
        public virtual int CurrentVersion => 3;

        private static BigInteger Pow256 = BigInteger.ValueOf(2).Pow(256);

        public uint256 HashPrevBlock
        {
            get
            {
                return new uint256(this.header.AsSpan().Slice(4, 32));
            }
            set
            {
                value.ToBytes().AsSpan().CopyTo(this.header.AsSpan(4, 32));
            }
        }

        public uint Time
        {
            get
            {
                return BitConverter.ToUInt32(this.header.AsSpan().Slice(68, 4));
            }
            set
            {
                try
                {
                    var hspan = this.header.AsSpan(68, 4);

                    var bytes = BitConverter.GetBytes(value);
                    var bspn = bytes.AsSpan();
                    bspn.CopyTo(hspan);
                }
                catch (Exception e)
                {
                    throw;
                }
            }
        }

        public Target Bits
        {
            get
            {
                return BitConverter.ToUInt32(this.header.AsSpan().Slice(72, 4));
            }
            set
            {
                BitConverter.GetBytes(value).AsSpan().CopyTo(this.header.AsSpan(72, 4));
            }
        }

        public int Version
        {
            get
            {
                return BitConverter.ToInt32(this.header.AsSpan().Slice(0, 4));
            }
            set
            {
                BitConverter.GetBytes(value).AsSpan().CopyTo(this.header.AsSpan(0, 4));
            }
        }

        public uint Nonce
        {
            get { return BitConverter.ToUInt32(this.header.AsSpan().Slice(76, 4)); }
            set { BitConverter.GetBytes(value).AsSpan().CopyTo(this.header.AsSpan(76, 4)); }
        }

        public uint256 HashMerkleRoot
        {
            get
            {
                return new uint256(this.header.AsSpan().Slice(36, 32));
            }
            set
            {
                value.ToBytes().AsSpan().CopyTo(this.header.AsSpan(36, 32));
            }
        }

        public bool IsNull { get { return (this.Bits == 0); } }

        protected byte[] header;
        protected uint256[] hashes;

        public virtual long HeaderSize => Size;

        public DateTimeOffset BlockTime
        {
            get { return Utils.UnixTimeToDateTime(this.Time); }
            set { this.Time = Utils.DateTimeToUnixTime(value); }
        }

        [Obsolete("Please use the Load method outside of consensus.")]
        public BlockHeader()
        {
            this.header = new byte[Size];
        }

        public static BlockHeader Load(byte[] bytes, Network network)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            if (network == null)
                throw new ArgumentNullException(nameof(network));

            BlockHeader blockHeader = network.Consensus.ConsensusFactory.CreateBlockHeader();
            blockHeader.ReadWrite(bytes, network.Consensus.ConsensusFactory);

            return blockHeader;
        }

        #region IBitcoinSerializable Members

        public virtual void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.header);

            //stream.ReadWrite(ref this.version); 0 - 3
            //stream.ReadWrite(ref this.hashPrevBlock); 4 - 35
            //stream.ReadWrite(ref this.hashMerkleRoot); 36 - 67
            //stream.ReadWrite(ref this.time);  68 - 71
            //stream.ReadWrite(ref this.bits); 72 - 75
            //stream.ReadWrite(ref this.nonce); 76 - 79
        }

        #endregion IBitcoinSerializable Members

        /// <summary>Populates stream with items that will be used during hash calculation.</summary>
        protected virtual void ReadWriteHashingStream(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.header);

            //stream.ReadWrite(ref this.version);
            //stream.ReadWrite(ref this.hashPrevBlock);
            //stream.ReadWrite(ref this.hashMerkleRoot);
            //stream.ReadWrite(ref this.time);
            //stream.ReadWrite(ref this.bits);
            //stream.ReadWrite(ref this.nonce);
        }

        /// <summary>
        /// Generates the hash of a <see cref="BlockHeader"/> or uses cached one.
        /// </summary>
        /// <returns>A hash.</returns>
        public virtual uint256 GetHash()
        {
            uint256 hash = null;
            uint256[] hashes = this.hashes;

            if (hashes != null)
                hash = hashes[0];

            if (hash != null)
                return hash;

            using (var hs = new HashStream())
            {
                this.ReadWriteHashingStream(new BitcoinStream(hs, true));
                hash = hs.GetHash();
            }

            hashes = this.hashes;
            if (hashes != null)
            {
                hashes[0] = hash;
            }

            return hash;
        }

        /// <summary>
        /// Generates a hash for a proof-of-work block header.
        /// </summary>
        /// <returns>A hash.</returns>
        public virtual uint256 GetPoWHash()
        {
            return this.GetHash();
        }

        [Obsolete("Call PrecomputeHash(true, true) instead")]
        public void CacheHashes()
        {
            this.PrecomputeHash(true, true);
        }

        /// <summary>
        /// Precompute the block header hash so that later calls to <see cref="GetHash()"/> will returns the precomputed hash.
        /// </summary>
        /// <param name="invalidateExisting">If true, the previous precomputed hash is thrown away, else it is reused.</param>
        /// <param name="lazily">If <c>true</c>, the hash will be calculated and cached at the first call to GetHash(), else it will be immediately.</param>
        public void PrecomputeHash(bool invalidateExisting = false, bool lazily = false)
        {
            if (this.hashes == null || invalidateExisting)
                this.hashes = new uint256[1];

            if (!lazily && this.hashes[0] == null)
                this.hashes[0] = this.GetHash();
        }

        public bool CheckProofOfWork()
        {
            BigInteger bits = this.Bits.ToBigInteger();
            if ((bits.CompareTo(BigInteger.Zero) <= 0) || (bits.CompareTo(Pow256) >= 0))
                return false;

            return this.GetPoWHash() <= this.Bits.ToUInt256();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.GetHash().ToString();
        }

        /// <summary>
        /// Set time to consensus acceptable value.
        /// </summary>
        /// <param name="now">The expected date.</param>
        /// <param name="consensus">Consensus.</param>
        /// <param name="prev">Previous block.</param>
        public void UpdateTime(DateTimeOffset now, IConsensus consensus, ChainedHeader prev)
        {
            DateTimeOffset nOldTime = this.BlockTime;
            DateTimeOffset mtp = prev.GetMedianTimePast() + TimeSpan.FromSeconds(1);
            DateTimeOffset nNewTime = mtp > now ? mtp : now;

            if (nOldTime < nNewTime)
                this.BlockTime = nNewTime;

            // Updating time can change work required on testnet.
            if (consensus.PowAllowMinDifficultyBlocks)
                this.Bits = this.GetWorkRequired(consensus, prev);
        }

        /// <summary>
        /// Set time to consensus acceptable value.
        /// </summary>
        /// <param name="now">The expected date.</param>
        /// <param name="network">Network.</param>
        /// <param name="prev">Previous block.</param>
        public void UpdateTime(DateTimeOffset now, Network network, ChainedHeader prev)
        {
            this.UpdateTime(now, network.Consensus, prev);
        }

        public Target GetWorkRequired(Network network, ChainedHeader prev)
        {
            return this.GetWorkRequired(network.Consensus, prev);
        }

        public Target GetWorkRequired(IConsensus consensus, ChainedHeader prev)
        {
            return new ChainedHeader(this, this.GetHash(), prev).GetWorkRequired(consensus);
        }
    }
}