#if !NOFILEIO
using System.IO;
using NBitcoin.DataEncoders;
using Xunit;

namespace NBitcoin.Tests
{
    public class checkblock_tests
    {
        public checkblock_tests()
        {
            // The tests are related to Bitcoin.
            // Set these expected values accordingly.
            Transaction.TimeStamp = false;
            PowBlock.BlockSignature = false;
        }

        [Fact]
        [Trait("UnitTest", "UnitTest")]
        public void CanCalculateMerkleRoot()
        {
            PowBlock powBlock = new PowBlock();
            powBlock.ReadWrite(Encoders.Hex.DecodeData(File.ReadAllText(@"data\block169482.txt")));
            Assert.Equal(powBlock.Header.HashMerkleRoot, powBlock.GetMerkleRoot().Hash);
        }        
    }
}
#endif