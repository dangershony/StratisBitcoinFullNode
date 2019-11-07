using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Api.Responses
{
    public class GetAddressesResponse
    {
        public List<AddressModel> Addresses { get; set; }
    }
}