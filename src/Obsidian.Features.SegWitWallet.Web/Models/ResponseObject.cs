namespace Obsidian.Features.SegWitWallet.Web.Models
{
    public class ResponseObject<T>
    {
        public T ResponsePayload;

        public int Status;

        public string StatusText;
    }
}