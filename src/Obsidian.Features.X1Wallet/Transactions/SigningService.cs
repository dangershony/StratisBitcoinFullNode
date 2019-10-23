using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;

namespace Obsidian.Features.X1Wallet.Transactions
{
    public class SigningService
    {
        Network network;

        public SigningService(Network network)
        {
            this.network = network;
        }

        public void SignInputs(Transaction transaction, Key[] keys, StakingCoin[] coins)
        {
            for (var i=0; i<transaction.Inputs.Count;i++)
            {
                var txin = transaction.Inputs[i];
                var key = keys[i];
                var coin = coins[i];
                SignInput(txin, key, coin, transaction);
            }
        }

        void SignInput(TxIn txin, Key key, StakingCoin coin, Transaction transaction)
        {
            uint256 signatureHash = SignatureHash(this.network, coin, transaction, SigHash.All);
            uint256 signatureHashOld = Script.SignatureHash(this.network, coin, transaction);
            Debug.Assert(signatureHash == signatureHashOld);

            var signature = key.Sign(signatureHash, SigHash.All);
            ECDSASignature ecdsaSig = signature.Signature;
            byte[] derSig = ecdsaSig.ToDER();
            byte[] finalSig = new byte[derSig.Length + 1];
            Array.Copy(derSig, 0, finalSig, 0, derSig.Length);
            finalSig[finalSig.Length - 1] = (byte)SigHash.All;
            var witScript = new WitScript(Op.GetPushOp(signature.ToBytes()),
                Op.GetPushOp(key.PubKey.Compress().ToBytes()));
            txin.WitScript = witScript;
        }

        static uint256 SignatureHash(Network network, StakingCoin coin, Transaction tx, SigHash nHashType = SigHash.All)
        {
            //IndexedTxIn[] indexedInputs = new IndexedTxIn[txTo.Inputs.Count];

            //for (var i = 0; i < txTo.Inputs.Count; i++)
            //{
            //    indexedInputs[i] = new IndexedTxIn { Index = i,TxIn =}
            //}
            IndexedTxIn input = tx.Inputs.AsIndexedInputs().FirstOrDefault(i => i.PrevOut == coin.Outpoint);

            if (input == null)
                throw new ArgumentException("coin should be spent spent in tx", nameof(coin));

            return IndexedTxInGetSignatureHash(network, tx, (int)input.Index, coin, nHashType);
        }


        public IEnumerable<IndexedTxIn> AsIndexedInputs(TxInList txInList, Transaction tx)
        {

            // We want i as the index of txIn in Intputs[], not index in enumerable after where filter
            return txInList.Select((r, i) => new IndexedTxIn()
            {
                TxIn = r,
                Index = (uint)i,
                Transaction = tx
            });
        }

        static uint256 IndexedTxInGetSignatureHash(Network network, Transaction tx, int index, StakingCoin coin, SigHash sigHash = SigHash.All)
        {
            Script scriptCode = GetScriptCode(coin.Address.ScriptPubKeyFromBech32Safe());

            return SignatureHash(scriptCode, tx, index, sigHash, coin.TxOut.Value);
        }

        static Script GetScriptCode(Script scriptPubKey)
        {
            WitKeyId key = PayToWitPubKeyHashExtractScriptPubKeyParameters(scriptPubKey);
            KeyId keyId = key.AsKeyId();
            var scriptCode = keyId.ScriptPubKey;
            Debug.Assert(scriptPubKey != scriptCode);
            return scriptCode;
        }

        static WitKeyId PayToWitPubKeyHashExtractScriptPubKeyParameters(Script scriptPubKey)
        {
            var data = new byte[20];
            Array.Copy(scriptPubKey.ToBytes(true), 2, data, 0, 20);
            return new WitKeyId(data);
        }

        static uint256 SignatureHash(Script scriptCode, Transaction tx, int nIn, SigHash nHashType, Money amount)
        {
            if (amount == null)
                throw new ArgumentException("The amount of the output being signed must be provided", nameof(amount));

            uint256 hashPrevouts = uint256.Zero;
            uint256 hashSequence = uint256.Zero;
            uint256 hashOutputs = uint256.Zero;

            if ((nHashType & SigHash.AnyoneCanPay) == 0)
            {
                hashPrevouts = GetHashPrevouts(tx);
            }

            if ((nHashType & SigHash.AnyoneCanPay) == 0 && ((uint)nHashType & 0x1f) != (uint)SigHash.Single && ((uint)nHashType & 0x1f) != (uint)SigHash.None)
            {
                hashSequence = GetHashSequence(tx);
            }

            if (((uint)nHashType & 0x1f) != (uint)SigHash.Single && ((uint)nHashType & 0x1f) != (uint)SigHash.None)
            {
                hashOutputs = GetHashOutputs(tx);
            }
            else if (((uint)nHashType & 0x1f) == (uint)SigHash.Single && nIn < tx.Outputs.Count)
            {
                BitcoinStream ss = CreateHashWriter(HashVersion.Witness);
                ss.ReadWrite(tx.Outputs[nIn]);
                hashOutputs = GetHash(ss);
            }

            BitcoinStream sss = CreateHashWriter(HashVersion.Witness);
            // Version
            sss.ReadWrite(tx.Version);
            // Input prevouts/nSequence (none/all, depending on flags)
            sss.ReadWrite(hashPrevouts);
            sss.ReadWrite(hashSequence);
            // The input being signed (replacing the scriptSig with scriptCode + amount)
            // The prevout may already be contained in hashPrevout, and the nSequence
            // may already be contain in hashSequence.
            sss.ReadWrite(tx.Inputs[nIn].PrevOut);
            sss.ReadWrite(scriptCode);
            sss.ReadWrite(amount.Satoshi);
            sss.ReadWrite((uint)tx.Inputs[nIn].Sequence);
            // Outputs (none/one/all, depending on flags)
            sss.ReadWrite(hashOutputs);
            // Locktime
            sss.ReadWriteStruct(tx.LockTime);
            // Sighash type
            sss.ReadWrite((uint)nHashType);

            return GetHash(sss);

        }

        internal static uint256 GetHashPrevouts(Transaction txTo)
        {
            uint256 hashPrevouts;
            BitcoinStream ss = CreateHashWriter(HashVersion.Witness);
            foreach (TxIn input in txTo.Inputs)
            {
                ss.ReadWrite(input.PrevOut);
            }
            hashPrevouts = GetHash(ss);
            return hashPrevouts;
        }

        internal static uint256 GetHashOutputs(Transaction txTo)
        {
            uint256 hashOutputs;
            BitcoinStream ss = CreateHashWriter(HashVersion.Witness);
            foreach (TxOut txout in txTo.Outputs)
            {
                ss.ReadWrite(txout);
            }
            hashOutputs = GetHash(ss);
            return hashOutputs;
        }

        internal static uint256 GetHashSequence(Transaction txTo)
        {
            uint256 hashSequence;
            BitcoinStream ss = CreateHashWriter(HashVersion.Witness);
            foreach (TxIn input in txTo.Inputs)
            {
                ss.ReadWrite((uint)input.Sequence);
            }
            hashSequence = GetHash(ss);
            return hashSequence;
        }

        private static BitcoinStream CreateHashWriter(HashVersion version)
        {
            var hs = new HashStream();
            var stream = new BitcoinStream(hs, true);
            stream.Type = SerializationType.Hash;
            stream.TransactionOptions = version == HashVersion.Original ? TransactionOptions.None : TransactionOptions.Witness;
            return stream;
        }

        private static uint256 GetHash(BitcoinStream stream)
        {
            uint256 preimage = ((HashStream)stream.Inner).GetHash();
            stream.Inner.Dispose();
            return preimage;
        }

    }
}
