using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    public static class P2WPKHAddressExtensions
    {
        public static X1WalletMetadataFile Metadata;

        public static Script GetPaymentScript(this P2WPKHAddress address)
        {
            var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript;
        }

        public static byte[] GetPaymentScriptBytes(this P2WPKHAddress address)
        {
            var hash160 = Hashes.Hash160(address.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript.ToBytes();
        }

        public static TransactionData[] GetUnspentTransactions(this P2WPKHAddress address)
        {
            if (Metadata.Transactions.TryGetValue(address.HashHex, out List<TransactionData> txs))
            {
                return txs.Where(t => !t.IsSpent()).ToArray();
            }
            return new TransactionData[0];
        }

        public static bool IsUsed(this P2WPKHAddress address)
        {
            if (Metadata.UsedAddresses.Contains(address.HashHex))
                return true;
            return false;
        }

        public static List<TransactionData> GetTransactions(this P2WPKHAddress address)
        {
            if (Metadata.Transactions.TryGetValue(address.HashHex, out List<TransactionData> txs))
            {
                return txs;
            }
            return new List<TransactionData>();
        }


        public static (Money ConfirmedAmount, Money UnConfirmedAmount) GetBalances(this P2WPKHAddress address)
        {
            if (Metadata.Transactions.TryGetValue(address.HashHex, out List<TransactionData> txs))
            {
                long confirmed = txs.Sum(t => t.GetUnspentAmount(true));
                long total = txs.Sum(t => t.GetUnspentAmount(false));
                return (confirmed, total - confirmed);
            }
            else
            {
                return (Money.Zero, Money.Zero);
            }
        }

    }
}