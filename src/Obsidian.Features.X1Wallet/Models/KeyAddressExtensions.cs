using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.X1Wallet.Models
{
    public static class KeyAddressExtensions
    {
        public static Script GetPaymentScript(this KeyAddressOld keyAddress)
        {
            var hash160 = Hashes.Hash160(keyAddress.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript;
        }

        public static byte[] GetPaymentScriptBytes(this KeyAddressOld keyAddress)
        {
            var hash160 = Hashes.Hash160(keyAddress.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript.ToBytes();
        }

        public static TransactionData[] GetUnspentTransactions(this KeyAddressOld keyAddress)
        {
            if (keyAddress.Transactions == null)
            {
                return new TransactionData[0];
            }

            return keyAddress.Transactions.Where(t => !t.IsSpent()).ToArray();
        }

        


        public static (Money ConfirmedAmount, Money UnConfirmedAmount) GetBalances(this KeyAddressOld keyAddress)
        {
            long confirmed = keyAddress.Transactions.Sum(t => t.GetUnspentAmount(true));
            long total = keyAddress.Transactions.Sum(t => t.GetUnspentAmount(false));

            return (confirmed, total - confirmed);
        }

        public static HdAddress ToFakeHdAddress(this KeyAddressOld keyAddress)
        {
            var hd = new HdAddress
            {
                Address = keyAddress.Bech32,
                HdPath = HdOperations.CreateHdPath(keyAddress.CoinType, 0, keyAddress.IsChange,keyAddress.ScriptPubKey.GetHashCode()),

                Index = keyAddress.ScriptPubKey.GetHashCode(),
                Pubkey = new Script(keyAddress.CompressedPublicKey),
                ScriptPubKey = keyAddress.ScriptPubKey,
                Transactions = keyAddress.Transactions
            };
            return hd;
        }



    }
}