using System;
using System.Collections.Generic;

namespace Obsidian.Features.X1Wallet.Models.Wallet
{
    public class P2WpkhAddress : IEquatable<P2WpkhAddress>
    {
        /// <summary>
        /// Decrypts to the pure key, length 32 bytes.
        /// </summary>
        public byte[] EncryptedPrivateKey { get; set; }

        /// <summary>
        /// The compressed public key corresponding to the private key, length: 33 bytes.
        /// </summary>
        public byte[] CompressedPublicKey { get; set; }


        /// <summary>
        /// The string representation of the address.
        /// </summary>
        public string Address { get; set; }

       public AddressType AddressType { get; set; }



        public override bool Equals(object obj)
        {
            return Equals(obj as P2WpkhAddress);
        }

        public bool Equals(P2WpkhAddress other)
        {
            return other != null &&
                   this.Address == other.Address;
        }

        public override int GetHashCode()
        {
            return -1052816746 + EqualityComparer<string>.Default.GetHashCode(this.Address);
        }

        public static bool operator ==(P2WpkhAddress left, P2WpkhAddress right)
        {
            return EqualityComparer<P2WpkhAddress>.Default.Equals(left, right);
        }

        public static bool operator !=(P2WpkhAddress left, P2WpkhAddress right)
        {
            return !(left == right);
        }
    }
}
