using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Api.Responses
{
    public class ExportKeysResponse
    {
        public string Message { get; set; }
        public List<ExportedAddress> ExportedAddresses { get; set; }
    }
}