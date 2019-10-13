using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Newtonsoft.Json.Serialization;
using Stratis.Bitcoin.Utilities.JsonConverters;

namespace Obsidian.Features.X1Wallet.Storage
{
    public class WalletContractResolver : CamelCasePropertyNamesContractResolver
    {
        protected override JsonPrimitiveContract CreatePrimitiveContract(Type objectType)
        {
            JsonPrimitiveContract contract = base.CreatePrimitiveContract(objectType);
            if (objectType == typeof(uint256))
            {
                contract.Converter = new UInt256JsonConverter();
            }

            return contract;
        }
    }
}
