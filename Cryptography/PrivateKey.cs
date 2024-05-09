
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ipfs.Cryptography.Proto
{
    public partial class PrivateKey : IEquatable<PrivateKey>
    {
        /// <summary>
        /// Serialize the public key according to
        /// https://github.com/libp2p/specs/blob/master/peer-ids/peer-ids.md
        /// </summary>
        /// <returns>The byte array with the protobuffered public key</returns>
        public byte[] Serialize()
        {
            using (var ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, this);
                ms.Position = 0;
                return ms.ToArray();
            }
        }

        public static PrivateKey Deserialize(Stream s)
            => ProtoBuf.Serializer.Deserialize<PrivateKey>(s);

        /// <summary>
        /// Deserialize the public key according to
        /// https://github.com/libp2p/specs/blob/master/peer-ids/peer-ids.md
        /// </summary>
        /// <param name="data"></param>
        /// <returns>The protobuffered public key</returns>
        public static PrivateKey Deserialize(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data, false))
            {
                return Deserialize(ms);
            }
        }

        public static implicit operator AsymmetricKeyParameter(PrivateKey pk)
        {
            AsymmetricKeyParameter akp;

            switch(pk.Type)
            {
                case KeyType.RSA:
                    // RSA private keys are encoded as PKCS#1
                    Asn1Sequence seq = Asn1Sequence.GetInstance(pk.Data);
                    RsaPrivateKeyStructure rsa = RsaPrivateKeyStructure.GetInstance(seq);
                    if (seq.Count != 9)
                        throw new InvalidOperationException("malformed sequence in RSA private key");

                    akp = new RsaPrivateCrtKeyParameters(
                            rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent,
                            rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2,
                            rsa.Coefficient);
                    return akp;
                case KeyType.Ed25519:
                    // Ed25519 keys are encoded with <private key><public key>[<public key>]
                    // Extension: Only <private key>, public key can be derived.
                    if (pk.Data.Length != 32 && pk.Data.Length != 64 && pk.Data.Length != 96)
                        throw new InvalidOperationException("Data has wrong length");
                    akp = new Ed25519PrivateKeyParameters(pk.Data, 0);
                    return akp;
                default: 
                    throw new NotImplementedException();
            }
        }

        public override bool Equals(object obj)
            => Equals(obj as PrivateKey);

        public bool Equals(PrivateKey other)
            => other is not null && Type == other.Type && other.Data.SequenceEqual(Data);

        public override int GetHashCode()
            => HashCode.Combine(Type, Data);

        public static bool operator ==(PrivateKey left, PrivateKey right)
            => EqualityComparer<PrivateKey>.Default.Equals(left, right);

        public static bool operator !=(PrivateKey left, PrivateKey right)
            => !(left == right);

    }
}