using System;
using System.Collections.Generic;
using System.Text;

namespace Obsidian.Features.X1Wallet.Models
{
    public class ImportKeysRequest
    {
        public string WalletPassphrase { get; set; }
        public string Keys { get; set; }
    }
}
