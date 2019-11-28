﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Consensus.Rules;

namespace Obsidian.Networks.ObsidianX.Rules
{
    /// <summary>
    /// Checks if transactions match the white-listing criteria. This rule and <see cref="ObsidianXOutputNotWhitelistedMempoolRule"/> must correspond.
    /// </summary>
    public class ObsidianXOutputNotWhitelistedRule : PartialValidationConsensusRule
    {
        public override Task RunAsync(RuleContext context)
        {
            var block = context.ValidationContext.BlockToValidate;
            var isPosBlock = block.Transactions.Count >= 2 && block.Transactions[1].IsCoinStake;

            foreach (var transaction in context.ValidationContext.BlockToValidate.Transactions)
            {
                if (transaction.IsCoinStake)
                    continue;

                if (transaction.IsCoinBase && isPosBlock)
                    continue;

                foreach (var output in transaction.Outputs)
                {

                    if (IsOutputWhitelisted(output))
                        continue; 
                   

                    this.Logger.LogTrace($"(-)[FAIL_{nameof(ObsidianXOutputNotWhitelistedRule)}]".ToUpperInvariant());
                    ObsidianXConsensusErrors.OutputNotWhitelisted.Throw();
                }
            }

            return Task.CompletedTask;
        }

        public static bool IsOutputWhitelisted(TxOut txOut)
        {
            if (txOut == null || txOut.ScriptPubKey == null || txOut.ScriptPubKey.Length == 0)
                throw new ArgumentException("This method expects a TxOut with a non-empty ScriptPubKey.");

            byte[] raw = txOut.ScriptPubKey.ToBytes();

            const int witnessVersion = 0;

            // P2WPKH
            if (raw.Length == 22 && raw[0] == witnessVersion && raw[1] == 20)
                return true;

            // P2WSH
            if (raw.Length == 34 && raw[0] == witnessVersion && raw[1] == 32)
                return true;

            // OP_RETURN
            if (raw[0] == (byte)OpcodeType.OP_RETURN)
                return true;

            return false;
        }
    }
}