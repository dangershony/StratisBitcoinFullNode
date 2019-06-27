using System;
using System.Net;

namespace Obsidian.Features.SegWitWallet
{
    public class SegWitWalletException : Exception
    {
        public HttpStatusCode HttpStatusCode;

        public SegWitWalletException(HttpStatusCode httpStatusCode, string message, Exception innerException) : base(message,innerException)
        {
            this.HttpStatusCode = httpStatusCode;
        }

        public override string ToString()
        {
            return $"Error {this.HttpStatusCode}: {base.ToString()}";
        }
    }
}
