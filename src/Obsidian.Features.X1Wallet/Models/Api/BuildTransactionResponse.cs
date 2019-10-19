using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Wallet.Models;

namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class BuildTransactionResponse
    {
        public Transaction Transaction;
        public string Hex;
        public Money Fee;
        public uint256 TransactionId;
        public int SerializedSize;
        public int VirtualSize;
    }

    public class BuildTransactionRequest
    {
        public string Password;
        public List<RecipientModel> Recipients;
        public string OpReturnData;
        public Money OpReturnAmount;
        public bool Sign;
    }
}
