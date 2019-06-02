using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Obsidian.ObsidianD
{
    public class ObsidianBIP9DeploymentsArray : BIP9DeploymentsArray
    {
        // The position of each deployment in the deployments array.
        public const int TestDummy = 0;
        public const int CSV = 1;
        public const int Segwit = 2;

        // The number of deployments.
        public const int NumberOfDeployments = 3;

        /// <summary>
        /// Constructs the BIP9 deployments array.
        /// </summary>
        public ObsidianBIP9DeploymentsArray() : base(NumberOfDeployments)
        {
        }

        /// <summary>
        /// Gets the deployment flags to set when the deployment activates.
        /// </summary>
        /// <param name="deployment">The deployment number.</param>
        /// <returns>The deployment flags.</returns>
        public override BIP9DeploymentFlags GetFlags(int deployment)
        {
            BIP9DeploymentFlags flags = new BIP9DeploymentFlags();

            switch (deployment)
            {
                case CSV:
                    // Start enforcing BIP68 (sequence locks), BIP112 (CHECKSEQUENCEVERIFY) and BIP113 (Median Time Past) using versionbits logic.
                    flags.ScriptFlags = ScriptVerify.CheckSequenceVerify;
                    flags.LockTimeFlags = Transaction.LockTimeFlags.VerifySequence | Transaction.LockTimeFlags.MedianTimePast;
                    break;
                case Segwit:
                    // Start enforcing WITNESS rules using versionbits logic.
                    flags.ScriptFlags = ScriptVerify.Witness;
                    break;
            }

            return flags;
        }
    }
}
