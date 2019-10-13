using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Policy;
using Obsidian.Features.X1Wallet.Models;
using Obsidian.Features.X1Wallet.Storage;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
using Stratis.Bitcoin.Utilities;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet
{
    /// <summary>
    /// A handler with functionality related to transaction operations.
    /// </summary>
    /// <remarks>
    /// This will uses the <see cref="IWalletFeePolicy"/> and the <see cref="TransactionBuilder"/>.
    /// TODO: Move also the broadcast transaction to this class
    /// TODO: Implement lockUnspents
    /// TODO: Implement subtractFeeFromOutputs
    /// </remarks>
    public class TransactionHandler : IWalletTransactionHandler
    {
        private readonly ILogger logger;

        private readonly Network network;

        protected readonly StandardTransactionPolicy TransactionPolicy;

        readonly WalletManagerWrapper walletManagerWrapper;

        private readonly IWalletFeePolicy walletFeePolicy;

        public TransactionHandler(
            ILoggerFactory loggerFactory,
            WalletManagerWrapper walletManager,
            IWalletFeePolicy walletFeePolicy,
            Network network,
            StandardTransactionPolicy transactionPolicy)
        {
            this.network = network;
            this.walletManagerWrapper = (WalletManagerWrapper)walletManager;
            this.walletFeePolicy = walletFeePolicy;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);

            this.TransactionPolicy = transactionPolicy;
        }



        /// <inheritdoc />
        public Transaction BuildTransaction(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            if (context.Shuffle)
                context.TransactionBuilder.Shuffle();

            Transaction transaction = context.TransactionBuilder.BuildTransaction(context.Sign);

            if (context.TransactionBuilder.Verify(transaction, out TransactionPolicyError[] errors))
                return transaction;

            string errorsMessage = string.Join(" - ", errors.Select(s => s.ToString()));
            this.logger.LogError($"Build transaction failed: {errorsMessage}");
            throw new WalletException($"Could not build the transaction. Details: {errorsMessage}");
        }

        /// <inheritdoc />
        public void FundTransaction(TransactionBuildContext context, Transaction transaction)
        {
            if (context.Recipients.Any())
                throw new WalletException("Adding outputs is not allowed.");

            // Turn the txout set into a Recipient array
            context.Recipients.AddRange(transaction.Outputs
                .Select(s => new Recipient
                {
                    ScriptPubKey = s.ScriptPubKey,
                    Amount = s.Value,
                    SubtractFeeFromAmount = false // default for now
                }));

            context.AllowOtherInputs = true;

            foreach (TxIn transactionInput in transaction.Inputs)
                context.SelectedInputs.Add(transactionInput.PrevOut);

            Transaction newTransaction = this.BuildTransaction(context);

            if (context.ChangeAddress != null)
            {
                // find the position of the change and move it over.
                int index = 0;
                foreach (TxOut newTransactionOutput in newTransaction.Outputs)
                {
                    if (newTransactionOutput.ScriptPubKey == context.ChangeAddress.ScriptPubKey)
                    {
                        transaction.Outputs.Insert(index, newTransactionOutput);
                    }

                    index++;
                }
            }

            // TODO: copy the new output amount size (this also includes spreading the fee over all outputs)

            // copy all the inputs from the new transaction.
            foreach (TxIn newTransactionInput in newTransaction.Inputs)
            {
                if (!context.SelectedInputs.Contains(newTransactionInput.PrevOut))
                {
                    transaction.Inputs.Add(newTransactionInput);

                    // TODO: build a mechanism to lock inputs
                }
            }
        }

        /// <inheritdoc />
        public (Money maximumSpendableAmount, Money Fee) GetMaximumSpendableAmount(WalletAccountReference accountReference, FeeType feeType, bool allowUnconfirmed)
        {
            Guard.NotNull(accountReference, nameof(accountReference));
            Guard.NotEmpty(accountReference.WalletName, nameof(accountReference.WalletName));

            long maxSpendableAmount;
            using (var context = this.walletManagerWrapper.GetWalletContext(accountReference.WalletName))
            {
                // Get the total value of spendable coins in the account.
                maxSpendableAmount = context.WalletManager.GetAllSpendableTransactions(allowUnconfirmed ? 0 : 1).Sum(x => x.Transaction.Amount);
            }


            // Return 0 if the user has nothing to spend.
            if (maxSpendableAmount == Money.Zero)
            {
                return (Money.Zero, Money.Zero);
            }

            // Create a recipient with a dummy destination address as it's required by NBitcoin's transaction builder.
            List<Recipient> recipients = new[] { new Recipient { Amount = new Money(maxSpendableAmount), ScriptPubKey = new Key().ScriptPubKey } }.ToList();
            Money fee;

            try
            {
                // Here we try to create a transaction that contains all the spendable coins, leaving no room for the fee.
                // When the transaction builder throws an exception informing us that we have insufficient funds,
                // we use the amount we're missing as the fee.
                var context = new TransactionBuildContext(this.network)
                {
                    FeeType = feeType,
                    MinConfirmations = allowUnconfirmed ? 0 : 1,
                    Recipients = recipients,
                    AccountReference = accountReference
                };

                this.AddRecipients(context);
                this.AddCoins(context);
                this.AddFee(context);

                // Throw an exception if this code is reached, as building a transaction without any funds for the fee should always throw an exception.
                throw new WalletException("This should be unreachable; please find and fix the bug that caused this to be reached.");
            }
            catch (NotEnoughFundsException e)
            {
                fee = (Money)e.Missing;
            }

            return (maxSpendableAmount - fee, fee);
        }

        /// <inheritdoc />
        public Money EstimateFee(TransactionBuildContext context)
        {
            this.InitializeTransactionBuilder(context);

            return context.TransactionFee;
        }

        /// <summary>
        /// Initializes the context transaction builder from information in <see cref="TransactionBuildContext"/>.
        /// </summary>
        /// <param name="context">Transaction build context.</param>
        protected virtual void InitializeTransactionBuilder(TransactionBuildContext context)
        {
            Guard.NotNull(context, nameof(context));
            Guard.NotNull(context.Recipients, nameof(context.Recipients));
            Guard.NotNull(context.AccountReference, nameof(context.AccountReference));

            // If inputs are selected by the user, we just choose them all.
            if (context.SelectedInputs != null && context.SelectedInputs.Any())
            {
                context.TransactionBuilder.CoinSelector = new AllCoinsSelector();
            }

            this.AddRecipients(context);
            this.AddOpReturnOutput(context);
            this.AddCoins(context);
            this.AddSecrets(context);
            this.FindChangeAddress(context);
            this.AddFee(context);

            if (context.Time.HasValue)
                context.TransactionBuilder.SetTimeStamp(context.Time.Value);
        }

        void AddSecrets(TransactionBuildContext context)
        {
            if (!context.Sign)
                return;

            using (var context2 = this.walletManagerWrapper.GetWalletContext(context.AccountReference.WalletName))
            {
                var addresses = context2.WalletManager.GetAllAddresses();
                // TODO: only decrypt the keys needed
                var keys = addresses
                    .Select(a => VCL.DecryptWithPassphrase(context.WalletPassword, a.Value.EncryptedPrivateKey))
                    .Select(privateKeyBytes => new Key(privateKeyBytes))
                    .ToArray();

                context.TransactionBuilder.AddKeys(keys);
            }

        }

      
        void FindChangeAddress(TransactionBuildContext context)
        {
            using (var walletContext = this.walletManagerWrapper.GetWalletContext(context.AccountReference.WalletName))
            {
                P2WpkhAddress changeAddress = walletContext.WalletManager.GetUnusedAddress();
                
                if(changeAddress == null)
                {
                    throw new X1WalletException(System.Net.HttpStatusCode.BadRequest,
                        $"The wallet doesn't have any unused addresses left to provide an unused change address for this transaction.",
                        null);
                }
                context.TransactionBuilder.SetChange(changeAddress.ScriptPubKeyFromPublicKey());
            }
        }

        void AddCoins(TransactionBuildContext context)
        {
            using (var walletContext = this.walletManagerWrapper.GetWalletContext(context.AccountReference.WalletName))
            {
                var allSpendableCoins = walletContext.WalletManager.GetBudget(out Balance _);
                context.TransactionBuilder.AddCoins(allSpendableCoins);
            }
        }

        protected virtual void AddRecipients(TransactionBuildContext context)
        {
            foreach (Recipient recipient in context.Recipients)
                context.TransactionBuilder.Send(recipient.ScriptPubKey, recipient.Amount);
        }

        void AddFee(TransactionBuildContext context)
        {
            Money fee;
            Money minTrxFee = new Money(this.network.MinTxFee, MoneyUnit.Satoshi);

            // If the fee hasn't been set manually, calculate it based on the fee type that was chosen.
            if (context.TransactionFee == null)
            {
                FeeRate feeRate = context.OverrideFeeRate ?? this.walletFeePolicy.GetFeeRate(context.FeeType.ToConfirmations());
                fee = context.TransactionBuilder.EstimateFees(feeRate);

                // Make sure that the fee is at least the minimum transaction fee.
                fee = Math.Max(fee, minTrxFee);
            }
            else
            {
                if (context.TransactionFee < minTrxFee)
                {
                    throw new WalletException($"Not enough fees. The minimun fee is {minTrxFee}.");
                }

                fee = context.TransactionFee;
            }

            context.TransactionBuilder.SendFees(fee);
            context.TransactionFee = fee;
        }

        void AddOpReturnOutput(TransactionBuildContext context)
        {
            if (string.IsNullOrWhiteSpace(context.OpReturnData))
                return;

            byte[] bytes = Encoding.UTF8.GetBytes(context.OpReturnData);
            Script opReturnScript = TxNullDataTemplate.Instance.GenerateScriptPubKey(bytes);
            context.TransactionBuilder.Send(opReturnScript, context.OpReturnAmount ?? Money.Zero);
        }
    }


}
