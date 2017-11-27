﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Base.Deployments;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    public abstract class ConsensusRule
    {
        protected ILogger logger;

        public abstract void Validate(ContextInformation context);
    }

    public class WitnessCommitmentsRule : ConsensusRule
    {
        public override void Validate(ContextInformation context)
        {
            DeploymentFlags deploymentFlags = context.Flags;
            Block block = context.BlockValidationContext.Block;

            // Validation for witness commitments.
            // * We compute the witness hash (which is the hash including witnesses) of all the block's transactions, except the
            //   coinbase (where 0x0000....0000 is used instead).
            // * The coinbase scriptWitness is a stack of a single 32-byte vector, containing a witness nonce (unconstrained).
            // * We build a merkle tree with all those witness hashes as leaves (similar to the hashMerkleRoot in the block header).
            // * There must be at least one output whose scriptPubKey is a single 36-byte push, the first 4 bytes of which are
            //   {0xaa, 0x21, 0xa9, 0xed}, and the following 32 bytes are SHA256^2(witness root, witness nonce). In case there are
            //   multiple, the last one is used.
            bool fHaveWitness = false;
            if (deploymentFlags.ScriptFlags.HasFlag(ScriptVerify.Witness))
            {
                int commitpos = this.GetWitnessCommitmentIndex(block);
                if (commitpos != -1)
                {
                    bool malleated = false;
                    uint256 hashWitness = BlockWitnessMerkleRoot(block, ref malleated);

                    // The malleation check is ignored; as the transaction tree itself
                    // already does not permit it, it is impossible to trigger in the
                    // witness tree.
                    WitScript witness = block.Transactions[0].Inputs[0].WitScript;
                    if ((witness.PushCount != 1) || (witness.Pushes.First().Length != 32))
                    {
                        this.logger.LogTrace("(-)[BAD_WITNESS_NONCE_SIZE]");
                        ConsensusErrors.BadWitnessNonceSize.Throw();
                    }

                    byte[] hashed = new byte[64];
                    Buffer.BlockCopy(hashWitness.ToBytes(), 0, hashed, 0, 32);
                    Buffer.BlockCopy(witness.Pushes.First(), 0, hashed, 32, 32);
                    hashWitness = Hashes.Hash256(hashed);

                    if (!this.EqualsArray(hashWitness.ToBytes(), block.Transactions[0].Outputs[commitpos].ScriptPubKey.ToBytes(true).Skip(6).ToArray(), 32))
                    {
                        this.logger.LogTrace("(-)[WITNESS_MERKLE_MISMATCH]");
                        ConsensusErrors.BadWitnessMerkleMatch.Throw();
                    }

                    fHaveWitness = true;
                }
            }

            if (!fHaveWitness)
            {
                for (int i = 0; i < block.Transactions.Count; i++)
                {
                    if (block.Transactions[i].HasWitness)
                    {
                        this.logger.LogTrace("(-)[UNEXPECTED_WITNESS]");
                        ConsensusErrors.UnexpectedWitness.Throw();
                    }
                }
            }
        }

        private bool EqualsArray(byte[] a, byte[] b, int len)
        {
            for (int i = 0; i < len; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        private int GetWitnessCommitmentIndex(Block block)
        {
            int commitpos = -1;
            for (int i = 0; i < block.Transactions[0].Outputs.Count; i++)
            {
                if ((block.Transactions[0].Outputs[i].ScriptPubKey.Length >= 38) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[1] == 0x24) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[2] == 0xaa) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[3] == 0x21) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[4] == 0xa9) &&
                    (block.Transactions[0].Outputs[i].ScriptPubKey.ToBytes(true)[5] == 0xed))
                {
                    commitpos = i;
                }
            }

            return commitpos;
        }

        public static uint256 ComputeMerkleRoot(List<uint256> leaves, ref bool mutated)
        {
            uint256 hash = null;
            MerkleComputation(leaves, ref hash, ref mutated, -1, null);
            return hash;
        }

        public static uint256 BlockWitnessMerkleRoot(Block block, ref bool mutated)
        {
            List<uint256> leaves = new List<uint256>();
            leaves.Add(uint256.Zero); // The witness hash of the coinbase is 0.
            foreach (Transaction tx in block.Transactions.Skip(1))
                leaves.Add(tx.GetWitHash());

            return ComputeMerkleRoot(leaves, ref mutated);
        }

        private uint256 BlockMerkleRoot(Block block, ref bool mutated)
        {
            List<uint256> leaves = new List<uint256>(block.Transactions.Count);
            for (int s = 0; s < block.Transactions.Count; s++)
                leaves.Add(block.Transactions[s].GetHash());

            return ComputeMerkleRoot(leaves, ref mutated);
        }

        public static  void MerkleComputation(List<uint256> leaves, ref uint256 root, ref bool pmutated, int branchpos, List<uint256> pbranch)
        {
            if (pbranch != null)
                pbranch.Clear();

            if (leaves.Count == 0)
            {
                pmutated = false;
                root = uint256.Zero;
                return;
            }

            bool mutated = false;

            // count is the number of leaves processed so far.
            uint count = 0;

            // inner is an array of eagerly computed subtree hashes, indexed by tree
            // level (0 being the leaves).
            // For example, when count is 25 (11001 in binary), inner[4] is the hash of
            // the first 16 leaves, inner[3] of the next 8 leaves, and inner[0] equal to
            // the last leaf. The other inner entries are undefined.
            var inner = new uint256[32];

            for (int i = 0; i < inner.Length; i++)
                inner[i] = uint256.Zero;

            // Which position in inner is a hash that depends on the matching leaf.
            int matchLevel = -1;

            // First process all leaves into 'inner' values.
            while (count < leaves.Count)
            {
                uint256 h = leaves[(int)count];
                bool matchh = count == branchpos;
                count++;
                int level;

                // For each of the lower bits in count that are 0, do 1 step. Each
                // corresponds to an inner value that existed before processing the
                // current leaf, and each needs a hash to combine it.
                for (level = 0; (count & (((uint)1) << level)) == 0; level++)
                {
                    if (pbranch != null)
                    {
                        if (matchh)
                        {
                            pbranch.Add(inner[level]);
                        }
                        else if (matchLevel == level)
                        {
                            pbranch.Add(h);
                            matchh = true;
                        }
                    }
                    mutated |= (inner[level] == h);
                    var hash = new byte[64];
                    Buffer.BlockCopy(inner[level].ToBytes(), 0, hash, 0, 32);
                    Buffer.BlockCopy(h.ToBytes(), 0, hash, 32, 32);
                    h = Hashes.Hash256(hash);
                }

                // Store the resulting hash at inner position level.
                inner[level] = h;
                if (matchh)
                    matchLevel = level;
            }

            // Do a final 'sweep' over the rightmost branch of the tree to process
            // odd levels, and reduce everything to a single top value.
            // Level is the level (counted from the bottom) up to which we've sweeped.
            int levell = 0;

            // As long as bit number level in count is zero, skip it. It means there
            // is nothing left at this level.
            while ((count & (((uint)1) << levell)) == 0)
                levell++;

            uint256 hh = inner[levell];
            bool matchhh = matchLevel == levell;
            while (count != (((uint)1) << levell))
            {
                // If we reach this point, h is an inner value that is not the top.
                // We combine it with itself (Bitcoin's special rule for odd levels in
                // the tree) to produce a higher level one.
                if (pbranch != null && matchhh)
                    pbranch.Add(hh);

                var hash = new byte[64];
                Buffer.BlockCopy(hh.ToBytes(), 0, hash, 0, 32);
                Buffer.BlockCopy(hh.ToBytes(), 0, hash, 32, 32);
                hh = Hashes.Hash256(hash);

                // Increment count to the value it would have if two entries at this
                // level had existed.
                count += (((uint)1) << levell);
                levell++;

                // And propagate the result upwards accordingly.
                while ((count & (((uint)1) << levell)) == 0)
                {
                    if (pbranch != null)
                    {
                        if (matchhh)
                        {
                            pbranch.Add(inner[levell]);
                        }
                        else if (matchLevel == levell)
                        {
                            pbranch.Add(hh);
                            matchhh = true;
                        }
                    }

                    var hashh = new byte[64];
                    Buffer.BlockCopy(inner[levell].ToBytes(), 0, hashh, 0, 32);
                    Buffer.BlockCopy(hh.ToBytes(), 0, hashh, 32, 32);
                    hh = Hashes.Hash256(hashh);

                    levell++;
                }
            }
            // Return result.
            pmutated = mutated;
            root = hh;
        }
    }

    public class Bip34Rule : ConsensusRule
    {
        public override void Validate(ContextInformation context)
        {
            DeploymentFlags deploymentFlags = context.Flags;

            if (deploymentFlags.EnforceBIP34)
            {
                int nHeight = context.BestBlock?.Height + 1 ?? 0;
                Block block = context.BlockValidationContext.Block;

                Script expect = new Script(Op.GetPushOp(nHeight));
                Script actual = block.Transactions[0].Inputs[0].ScriptSig;
                if (!this.StartWith(actual.ToBytes(true), expect.ToBytes(true)))
                {
                    this.logger.LogTrace("(-)[BAD_COINBASE_HEIGHT]");
                    ConsensusErrors.BadCoinbaseHeight.Throw();
                }
            }
        }

        /// <summary>
        /// Compare two byte arrays and return <c>true</c> if the first array start with the same sequence bytes as the second array from the first position.
        /// </summary>
        /// <param name="bytes">The first array in the checking sequence.</param>
        /// <param name="subset">The second array in the checking sequence.</param>
        /// <returns><c>true</c> if the second array has the same elements as the first array from the first position.</returns>
        private bool StartWith(byte[] bytes, byte[] subset)
        {
            if (bytes.Length < subset.Length)
                return false;

            for (int i = 0; i < subset.Length; i++)
            {
                if (subset[i] != bytes[i])
                    return false;
            }

            return true;
        }
    }

    public class ConsensusRules
    {
    }
}
