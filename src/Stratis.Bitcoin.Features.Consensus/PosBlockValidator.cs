using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.Bitcoin.Features.Consensus
{
    public class PosBlockValidator
    {
        public static bool IsCanonicalBlockSignature(PowBlock powBlock, bool checkLowS)
        {
            if (BlockStake.IsProofOfWork(powBlock))
                return powBlock.BlockSignatur.IsEmpty();

            return checkLowS ?
                ScriptEvaluationContext.IsLowDerSignature(powBlock.BlockSignatur.Signature) :
                ScriptEvaluationContext.IsValidSignatureEncoding(powBlock.BlockSignatur.Signature);
        }

        public static bool EnsureLowS(BlockSignature blockSignature)
        {
            var signature = new ECDSASignature(blockSignature.Signature);
            if (!signature.IsLowS)
                blockSignature.Signature = signature.MakeCanonical().ToDER();
            return true;
        }
    }
}
