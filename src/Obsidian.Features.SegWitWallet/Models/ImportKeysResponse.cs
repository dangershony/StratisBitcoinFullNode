using System;
using System.Collections.Generic;
using System.Text;

namespace Obsidian.Features.X1Wallet.Models
{
    public class ImportKeysResponse
    {
        public string Message { get; set; }
        public List<string> ImportedAddresses { get; set; }
    }
}
