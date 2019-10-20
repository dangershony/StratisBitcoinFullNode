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
    sealed class TransactionService
    {
        readonly ILogger logger;
        readonly Network network;
        readonly WalletManagerFactory walletManagerFactory;
        readonly string walletName;
        readonly FeeRate fixedFeeRate;

        public TransactionService(
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


        public BuildTransactionResponse BuildTransaction(List<Recipient> recipients, bool sign, string passphrase = null, uint? transactionTimestamp = null, Burn burn = null)
        {
            var tx = this.network.CreateTransaction();

            // time
            if (transactionTimestamp.HasValue)
                tx.Time = transactionTimestamp.Value;

            // add recipients
            foreach (Recipient recipient in recipients)
                tx.Outputs.Add(new TxOut(recipient.Amount, recipient.ScriptPubKey));

            // op_return data
            if (burn != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(burn.Utf8String);
                Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
                tx.Outputs.Add(new TxOut(burn.Amount ?? Money.Zero, opReturnScript));
            }

            TxOut changeOutput = null;
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

                changeOutput = new TxOut(Money.Zero, changeAddress.ScriptPubKeyFromPublicKey());
                tx.Outputs.Add(changeOutput);
            }

            var fee = Money.Zero;
            fundTx:

            // add outputs
            StakingCoin[] coins = AddCoins(recipients, burn, fee.Satoshi).ToArray();

            tx.Inputs.Clear();
            foreach (var c in coins)
                tx.Inputs.Add(new TxIn(c.Outpoint));

            var virtualSize = tx.GetVirtualSize();
           
            var currentFee = this.fixedFeeRate.GetFee(virtualSize);
            this.logger.LogInformation(
                $"VirtualSize: {virtualSize}, CurrentFee: {currentFee}, PreviousFee: {fee}, Coins: {coins.Length}.");
            if (fee != currentFee)
            {
                fee = currentFee;
                goto fundTx;
            }

            long outgoing = tx.Outputs.Sum(x => x.Value.Satoshi);
            var sending = coins.Sum(x => x.Amount.Satoshi);
            var change = sending - outgoing;
            changeOutput.Value = Money.Satoshis(change);

            // signing
            if (sign)
            {
                var keys = DecryptKeys(coins);

                tx.Sign(this.network, keys, coins);
                Key[] DecryptKeys(StakingCoin[] selectedCoins)
                {
                    var keys = new Key[selectedCoins.Length];
                    for (var i = 0; i < keys.Length; i++)
                        keys[i] = new Key(VCL.DecryptWithPassphrase(passphrase, selectedCoins[i].EncryptedPrivateKey));
                    return keys;
                }
            }

            // this doesn't work if MinTxFee and MinRelaxTxFee are not equal
            //if (!txb.Verify(transaction, this.fixedFeeRate, out TransactionPolicyError[] errors))
            //{
            //    string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
            //    this.logger.LogError($"Build transaction failed: {errorsMessage}");
            //    throw new X1WalletException(System.Net.HttpStatusCode.BadRequest, $"Transaction verification failed: {errorsMessage}");
            //}

            var response = new BuildTransactionResponse
            {
                Transaction = tx,
                Hex = tx.ToHex(),
                Fee = fee,
                VirtualSize = virtualSize,
                SerializedSize = tx.GetSerializedSize(),
                TransactionId = tx.GetHash()
            };

            return response;
        }

        public BuildTransactionResponse BuildTransaction3(List<Recipient> recipients, bool sign, string passphrase = null, uint? transactionTimestamp = null, Burn burn = null)
        {
            var txb = CreateTransactionBuilder(recipients, sign, out var fee, out var virtualSize, passphrase, transactionTimestamp, burn);

            var transaction = txb.BuildTransaction(sign);

            // this doesn't work if MinTxFee and MinRelaxTxFee are not equal
            if (!txb.Verify(transaction, this.fixedFeeRate, out TransactionPolicyError[] errors))
            {
                string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
                this.logger.LogError($"Build transaction failed: {errorsMessage}");
                throw new X1WalletException(System.Net.HttpStatusCode.BadRequest, $"Transaction verification failed: {errorsMessage}");
            }

            var response = new BuildTransactionResponse
            {
                Transaction = transaction,
                Hex = transaction.ToHex(),
                Fee = fee,
                VirtualSize = virtualSize,
                SerializedSize = transaction.GetSerializedSize(),
                TransactionId = transaction.GetHash()
            };

            return response;
        }

        TransactionBuilder CreateTransactionBuilder(List<Recipient> recipients, bool sign, out Money fee, out int virtualSize, string passphrase = null, uint? transactionTimestamp = null, Burn burn = null)
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

            fee = Money.Zero;

            fundTx:

            // add outputs
            var coins = AddCoins(recipients, burn, fee.Satoshi).ToArray();
            txb.AddCoins(coins);

            var test = txb.BuildTransaction(false);
            virtualSize = txb.EstimateSize(test);
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
                txb.AddKeys(DecryptKeys(coins));

                Key[] DecryptKeys(StakingCoin[] selectedCoins)
                {
                    var keys = new Key[selectedCoins.Length];
                    for (var i = 0; i < keys.Length; i++)
                        keys[i] = new Key(VCL.DecryptWithPassphrase(passphrase, selectedCoins[i].EncryptedPrivateKey));
                    return keys;
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
