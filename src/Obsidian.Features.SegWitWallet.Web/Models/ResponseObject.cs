namespace Obsidian.Features.X1Wallet.SecureApi.Models
{
    public class ResponseObject<T>
    {
        public T ResponsePayload;

        public int Status;

        public string StatusText;
    }
}