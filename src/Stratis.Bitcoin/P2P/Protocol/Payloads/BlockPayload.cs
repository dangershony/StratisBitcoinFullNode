using NBitcoin;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// A block received after being asked with a getdata message.
    /// </summary>
    [Payload("block")]
    public class BlockPayload : BitcoinSerializablePayload<PowBlock>
    {
        public BlockPayload()
        {
        }

        public BlockPayload(PowBlock powBlock)
            : base(powBlock)
        {
        }
    }
}
