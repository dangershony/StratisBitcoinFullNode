using System;
using System.Net;

namespace Obsidian.Features.X1Wallet.Models
{
    public class X1WalletException : Exception
    {
        public HttpStatusCode HttpStatusCode;

        public X1WalletException(HttpStatusCode httpStatusCode, string message, Exception innerException) : base(message,innerException)
        {
            this.HttpStatusCode = httpStatusCode;
        }

        public override string ToString()
        {
            return $"Error {this.HttpStatusCode}: {base.ToString()}";
        }
    }
}
