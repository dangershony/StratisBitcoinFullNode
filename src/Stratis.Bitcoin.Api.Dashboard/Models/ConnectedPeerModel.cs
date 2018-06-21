namespace Stratis.Bitcoin.Api.Dashboard.Models
{
    /// <summary>
    /// Represents a connected peer.
    /// </summary>
    public class ConnectedPeerModel
    {
        /// <summary>The version this peer is running.</summary>
        public string Version { get; set; }

        /// <summary>The endpoint where this peer is located.</summary>
        public string RemoteSocketEndpoint { get; set; }

        /// <summary>The height of this connected peer's tip.</summary>
        public int TipHeight { get; set; }
    }
}
