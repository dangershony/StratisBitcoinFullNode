using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Features.Wallet;

namespace Obsidian.Features.SegWitWallet
{
    public static class KeyAddressExtensions
    {
        public static Script GetPaymentScript(this KeyAddress keyAddress)
        {
            var hash160 = Hashes.Hash160(keyAddress.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript;
        }

        public static byte[] GetPaymentScriptBytes(this KeyAddress keyAddress)
        {
            var hash160 = Hashes.Hash160(keyAddress.CompressedPublicKey).ToBytes();
            var paymentScript = new Script(OpcodeType.OP_0, Op.GetPushOp(hash160));
            return paymentScript.ToBytes();
        }

        public static TransactionData[] GetUnspentTransactions(this KeyAddress keyAddress)
        {
            if (keyAddress.Transactions == null)
            {
                return new TransactionData[0];
            }

            return keyAddress.Transactions.Where(t => !t.IsSpent()).ToArray();
        }

       
        public static (Money ConfirmedAmount, Money UnConfirmedAmount) GetBalances(this KeyAddress keyAddress)
        {
            long confirmed = keyAddress.Transactions.Sum(t => t.GetUnspentAmount(true));
            long total = keyAddress.Transactions.Sum(t => t.GetUnspentAmount(false));

            return (confirmed, total - confirmed);
        }

        public static bool IsChangeAddress(this KeyAddress keyAddress)
        {
            return keyAddress.UniqueIndex % 2 != 0;
        }

        public static HdAddress ToFakeHdAddress(this KeyAddress keyAddress)
        {
            var hd = new HdAddress
            {
                Address = keyAddress.Bech32,
                HdPath = HdOperations.CreateHdPath(keyAddress.CoinType, 0, keyAddress.IsChangeAddress(),
                    keyAddress.UniqueIndex),

                Index = keyAddress.UniqueIndex,
                Pubkey = new Script(new Op[]
                    {OpcodeType.OP_RETURN, Op.GetPushOp(Encoding.ASCII.GetBytes("HdAddress.PubKey"))}),
                ScriptPubKey = keyAddress.GetPaymentScript(),
                Transactions = keyAddress.Transactions
            };
            return hd;
        }

        public static HdAccount ToFakeHdAccount(this ICollection<KeyAddress> ndAddresses, KeyWallet wallet)
        {
            var accountIndex = 0;
            var account = new HdAccount
            {
                ExternalAddresses = new List<HdAddress>(),
                InternalAddresses = new List<HdAddress>(),
                Index = accountIndex,
                Name = $"account {accountIndex}",
                HdPath = HdOperations.GetAccountHdPath(ndAddresses.First().CoinType, accountIndex),
                CreationTime = wallet.CreationTime,
                ExtendedPubKey = "ExtendedPubKey: not supported",
            };
            foreach (var adr in ndAddresses)
                if (!adr.IsChangeAddress())
                    account.ExternalAddresses.Add(adr.ToFakeHdAddress());
                else
                    account.InternalAddresses.Add(adr.ToFakeHdAddress());
            return account;
        }
    }
}