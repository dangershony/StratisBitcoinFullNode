using System;

namespace Obsidian.Features.X1Wallet.Models.Api
{
    public class ExportedAddress
    {
        public string EncodedKey { get; set; }
        public string Address { get; set; }
        public string LabelUrlEncoded { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsChange { get; set; }
    }
}