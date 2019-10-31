using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Api.Requests;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;
using VisualCrypt.VisualCryptLight;

namespace Obsidian.Features.X1Wallet.Transactions
{
    sealed class TransactionService
    {
        const int SignatureVirtualSize = 28;
        const int ChangeOutputVirtualSize = 31;
        const int MinimumVirtualSize = 86;  // one input, one output, without signature and change output

        readonly long minimumPossibleFee;
        readonly long dustThreshold;

        readonly ILogger logger;
        readonly Network network;
        readonly WalletManagerFactory walletManagerFactory;
        readonly string walletName;
        readonly long feePer1000VBytes;

        public TransactionService(
            ILoggerFactory loggerFactory,
            WalletManagerFactory walletManagerFactory, string walletName,
            Network network)
        {
            this.network = network;
            this.walletManagerFactory = walletManagerFactory;
            this.walletName = walletName;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.feePer1000VBytes = Math.Max(network.MinTxFee, network.MinRelayTxFee);
            this.minimumPossibleFee = (MinimumVirtualSize + SignatureVirtualSize + ChangeOutputVirtualSize) * this.feePer1000VBytes / 1000;
            this.dustThreshold = this.minimumPossibleFee * 5;
        }

        public TransactionResponse BuildTransaction(List<Recipient> recipients, bool sign, string passphrase = null, uint? transactionTimestamp = null, List<Burn> burns = null)
        {
            var tx = this.network.CreateTransaction();

            // time
            if (transactionTimestamp.HasValue)
                tx.Time = transactionTimestamp.Value;

            // add recipients
            long recipientsAmount = 0;
            foreach (Recipient recipient in recipients)
            {
                recipientsAmount += recipient.Amount;
                tx.Outputs.Add(new TxOut(recipient.Amount, recipient.Address.ScriptPubKeyFromBech32Safe()));
            }

            // add burns
            long burnAmount = 0;
            if (burns != null)
                foreach (Burn burn in burns)
                {
                    burnAmount += burn.Amount;
                    tx.Outputs.Add(new TxOut(burn.Amount, TxNullDataTemplate.Instance.GenerateScriptPubKey(burn.Data)));
                }

            long totalSendAmount = recipientsAmount + burnAmount;


            // calculate size, fee and change amount
            long addedFee = this.minimumPossibleFee;

            long selectedAmount = 0;
            StakingCoin[] selectedCoins;

            while (true)
            {
                tx.Inputs.Clear();

                int selectedCount = 0;
               
                selectedCoins = AddCoins(totalSendAmount, addedFee).ToArray();

                foreach (var c in selectedCoins)
                {
                    selectedCount++;
                    selectedAmount += c.Amount;
                    tx.Inputs.Add(new TxIn(c.Outpoint));
                }

                var virtualSize = tx.GetVirtualSize() + selectedCount * SignatureVirtualSize + ChangeOutputVirtualSize;

                long requiredFee = virtualSize * this.feePer1000VBytes / 1000;
                
                if (addedFee == requiredFee)
                {
                    var change = selectedAmount - totalSendAmount - addedFee;
                    if (change > this.dustThreshold)
                    {
                        TxOut changeOutput = GetOutputForChange();
                        changeOutput.Value = change;
                        tx.Outputs.Add(changeOutput);
                    }
                    break;
                }
                    
                addedFee = requiredFee;
            }

            // signing
            if (sign)
            {
                var keys = DecryptKeys(selectedCoins, passphrase);
                SigningService.SignInputs(tx, keys, selectedCoins);
            }

            var response = new TransactionResponse
            {
                Transaction = tx,
                Hex = tx.ToHex(),
                Fee = addedFee,
                VirtualSize = tx.GetVirtualSize(),
                SerializedSize = tx.GetSerializedSize(),
                TransactionId = tx.GetHash(),
                BroadcastState = BroadcastState.NotSet
            };

            return response;
        }



        IEnumerable<StakingCoin> AddCoins(long totalAmountToSend, long fee)
        {
            long totalToSelect = totalAmountToSend + fee;

            IReadOnlyList<StakingCoin> budget;
            Balance balance;
            using (var walletContext = GetWalletContext())
            {
                budget = walletContext.WalletManager.GetBudget(out balance);
            }

            if (balance.Spendable < totalToSelect)
                throw new X1WalletException(System.Net.HttpStatusCode.BadRequest,
                    $"Required are at least {totalToSelect}, but spendable is only {balance.Spendable}");

            int pointer = 0;
            long selectedAmount = 0;
            while (selectedAmount < totalToSelect)
            {
                StakingCoin coin = budget[pointer++];
                selectedAmount += coin.Amount.Satoshi;
                yield return coin;
            }
        }

        TxOut GetOutputForChange()
        {
            using var walletContext = GetWalletContext();

            P2WpkhAddress changeAddress = walletContext.WalletManager.GetUnusedAddress();

            if (changeAddress == null)
            {
                changeAddress = walletContext.WalletManager.GetAllAddresses().First().Value;
                this.logger.LogWarning("Caution, the wallet has run out off unused addresses, and will now use a used address as change address.");
            }

            return new TxOut(0, changeAddress.ScriptPubKeyFromPublicKey());
        }

        static Key[] DecryptKeys(StakingCoin[] selectedCoins, string passphrase)
        {
            var keys = new Key[selectedCoins.Length];
            for (var i = 0; i < keys.Length; i++)
                keys[i] = new Key(VCL.DecryptWithPassphrase(passphrase, selectedCoins[i].EncryptedPrivateKey));
            return keys;
        }

        WalletContext GetWalletContext()
        {
            return this.walletManagerFactory.GetWalletContext(this.walletName);
        }
    }
}
