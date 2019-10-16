using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class ImportKeysResponse
    {
        public string Message { get; set; }
        public List<string> ImportedAddresses { get; set; }
    }
}
