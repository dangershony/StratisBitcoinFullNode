using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Obsidian.Features.X1Wallet.Feature;
using Obsidian.Features.X1Wallet.Models.Api;
using Obsidian.Features.X1Wallet.Models.Wallet;
using Obsidian.Features.X1Wallet.Staking;
using Obsidian.Features.X1Wallet.Tools;
using Stratis.Bitcoin.Features.Wallet.Broadcasting;
using Stratis.Bitcoin.Features.Wallet.Interfaces;
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
        readonly IBroadcasterManager broadcasterManager;


        public TransactionService(
            ILoggerFactory loggerFactory,
            WalletManagerFactory walletManagerFactory, string walletName,
            Network network,IBroadcasterManager broadcasterManager)
        {
            this.network = network;
            this.walletManagerFactory = walletManagerFactory;
            this.walletName = walletName;
            this.logger = loggerFactory.CreateLogger(GetType().FullName);
            this.fixedFeeRate = new FeeRate(Money.Satoshis(Math.Max(network.MinTxFee, network.MinRelayTxFee)));
            this.broadcasterManager = broadcasterManager;
        }

        public BuildTransactionResponse BuildTransaction(List<Recipient> recipients, bool sign, bool send, string passphrase = null, uint? transactionTimestamp = null, List<Burn> burns = null)
        {
            var tx = this.network.CreateTransaction();

            // time
            if (transactionTimestamp.HasValue)
                tx.Time = transactionTimestamp.Value;

            // add recipients
            foreach (Recipient recipient in recipients)
                tx.Outputs.Add(new TxOut(recipient.Amount, recipient.Address.ScriptPubKeyFromBech32Safe()));

            // op_return data
            if (burns != null)
                foreach (Burn burn in burns)
                    tx.Outputs.Add(new TxOut(burn.Amount, TxNullDataTemplate.Instance.GenerateScriptPubKey(burn.Data)));


            // set change address
            TxOut changeOutput = GetOutputForChange();
            tx.Outputs.Add(changeOutput);

            // calculate size, fee and change amount
            var fee = Money.Zero;
            fundTx:

            // add outputs
            StakingCoin[] coins = AddCoins(recipients, fee.Satoshi, burns).ToArray();

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
            changeOutput.Value = change;

            // signing
            if (sign)
            {
                var keys = DecryptKeys(coins, passphrase);
                //tx.Sign(this.network, keys, coins);
                SigningService.SignInputs(tx, keys, coins);
            }


            if (send)
                Send(tx);


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

        void Send(Transaction transaction)
        {
            this.broadcasterManager.BroadcastTransactionAsync(transaction).GetAwaiter().GetResult();

            TransactionBroadcastEntry transactionBroadCastEntry = this.broadcasterManager.GetTransaction(transaction.GetHash());

            if (transactionBroadCastEntry.State == State.CantBroadcast)
            {
                throw new X1WalletException(HttpStatusCode.BadRequest, transactionBroadCastEntry.ErrorMessage, new Exception("Transaction Exception"));
            }
        }

        IEnumerable<StakingCoin> AddCoins(List<Recipient> recipients, long fee, List<Burn> burns = null)
        {
            long sendAmount = recipients.Sum(s => s.Amount) + fee;
            long burnAmount = burns?.Sum(b => b.Amount) ?? 0;
            long total = sendAmount + burnAmount + fee;

            IReadOnlyList<StakingCoin> budget;
            using (var walletContext = GetWalletContext())
            {
                budget = walletContext.WalletManager.GetBudget(out Balance _).OrderByDescending(o => o.Amount)
                    .ToArray();
            }

            int pointer = 0;
            long selectedAmount = 0;
            while (selectedAmount < total)
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
