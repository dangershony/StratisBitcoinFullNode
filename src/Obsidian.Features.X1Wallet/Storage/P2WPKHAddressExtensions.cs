using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using Obsidian.Features.X1Wallet.Models;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Storage
{
    public static class P2WPKHAddressExtensions
    {
        public static X1WalletMetadataFile Metadata;

        public static Script GetPaymentScript(this P2WpkhAddress address)
        {
            var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript;
        }

        public static byte[] GetPaymentScriptBytes(this P2WpkhAddress address)
        {
            var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript.ToBytes();
        }

       

        

        public static Dictionary<string, TransactionMetadata> GetTransactionsByAddress(this P2WpkhAddress address)
        {
            Dictionary<string, TransactionMetadata> transactions = null;
            foreach (BlockMetadata block in Metadata.Blocks.Values)
            {
                foreach (TransactionMetadata tx in block.ConfirmedTransactions.Values)
                {
                    foreach (UtxoMetadata utxo in tx.ReceivedUtxos)
                    {
                        if (utxo.HashHex == address.HashHex)
                        {
                            if (transactions == null)
                                transactions = new Dictionary<string, TransactionMetadata>();
                            transactions.Add(tx.HashTx, tx);
                            break;
                        }
                    }

                }
            }

            return transactions;
        }

       

       

    }
}