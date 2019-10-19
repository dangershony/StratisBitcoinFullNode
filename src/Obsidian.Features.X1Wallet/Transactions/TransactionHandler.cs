using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Transactions
{
    sealed class TransactionHandler
    {
        readonly ILogger logger;
        readonly Network network;
        readonly WalletManagerFactory walletManagerFactory;
        readonly string walletName;
        readonly FeeRate fixedFeeRate;

        public TransactionHandler(
            ILoggerFactory loggerFactory,
            WalletManagerFactory walletManagerFactory, string walletName,
            Network network)
        {
            this.network = network;
            this.walletManagerFactory = walletManagerFactory;
            this.walletName = walletName;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.fixedFeeRate = new FeeRate(Money.Satoshis(Math.Max(network.MinTxFee, network.MinRelayTxFee)));
        }


        public Money EstimateFee(List<Recipient> recipients, Burn burn = null)
        {
            var txb = CreateTransactionBuilder(recipients, false, burn: burn);
            return txb.EstimateFees(this.fixedFeeRate);
        }

        public Transaction BuildTransaction(List<Recipient> recipients, bool sign, string passphrase = null, uint? transactionTimestamp = null, Burn burn = null)
        {
            var txb = CreateTransactionBuilder(recipients, sign, passphrase, transactionTimestamp, burn);

            var transaction = txb.BuildTransaction(sign);

            // this doesn't work if MinTxFee and MinRelaxTxFee are not equal
            //if (!txb.Verify(transaction, this.fixedFeeRate, out TransactionPolicyError[] errors))
            //{
            //    string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
            //    this.logger.LogError($"Build transaction failed: {errorsMessage}");
            //    throw new X1WalletException(System.Net.HttpStatusCode.BadRequest, $"Transaction verification failed: {errorsMessage}");
            //}

            return transaction;
        }

        TransactionBuilder CreateTransactionBuilder(List<Recipient> recipients, bool sign, string passphrase = null, uint? transactionTimestamp = null, Burn burn = null)
        {
            var txb = new TransactionBuilder(this.network) { CoinSelector = new AllCoinsSelector() };

            // time
            if (transactionTimestamp.HasValue)
                txb.SetTimeStamp(transactionTimestamp.Value);

            // add recipients
            foreach (Recipient recipient in recipients)
                txb.Send(recipient.ScriptPubKey, recipient.Amount);

            // op_return data
            if (burn != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(burn.Utf8String);
                Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
                txb.Send(opReturnScript, burn.Amount ?? Money.Zero);
            }

            // set change address
            using (var walletContext = GetWalletContext())
            {
                P2WpkhAddress changeAddress = walletContext.WalletManager.GetUnusedAddress();

                if (changeAddress == null)
                {
                    changeAddress = walletContext.WalletManager.GetAllAddresses().First().Value;
                    this.logger.LogWarning(
                        "Caution, the wallet has run out off unused addressed, and will now use a used address as change address.");
                }
                txb.SetChange(changeAddress.ScriptPubKeyFromPublicKey());
            }

            Money fee = Money.Zero;

            fundTx:

            // add outputs
            var coins = AddCoins(recipients, burn, fee.Satoshi);
            txb.AddCoins(coins);

            var test = txb.BuildTransaction(false);
            var virtualSize = txb.EstimateSize(test);
            var currentFee = this.fixedFeeRate.GetFee(virtualSize);
            if (fee != currentFee)
            {
                fee = currentFee;
                goto fundTx;
            }
            txb.SendFees(fee);

            // signing
            if (sign)
            {
                txb.AddKeys(DecryptKeys(coins).ToArray());

                IEnumerable<Key> DecryptKeys(IEnumerable<StakingCoin> selectedCoins)
                {
                    foreach (var c in selectedCoins)
                        yield return new Key(VCL.DecryptWithPassphrase(passphrase, c.EncryptedPrivateKey));
                }
            }

            return txb;
        }

        IEnumerable<StakingCoin> AddCoins(List<Recipient> recipients, Burn burn, long fee)
        {
            long requiredAmount = recipients.Sum(s => s.Amount) + fee;
            if (burn != null && burn.Amount != null)
                requiredAmount += burn.Amount.Satoshi;

            IReadOnlyList<StakingCoin> budget;
            using (var walletContext = GetWalletContext())
            {
                budget = walletContext.WalletManager.GetBudget(out Balance _).OrderByDescending(o => o.Amount)
                    .ToArray();
            }

            int pointer = 0;
            long selectedAmount = 0;
            while (selectedAmount < requiredAmount)
            {
                StakingCoin coin = budget[pointer++];
                selectedAmount += coin.Amount.Satoshi;
                yield return coin;
            }
        }

        WalletContext GetWalletContext()
        {
            return this.walletManagerFactory.GetWalletContext(this.walletName);
        }
    }
}
