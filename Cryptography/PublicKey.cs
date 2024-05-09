
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ipfs.Cryptography.Proto
{
    public partial class PublicKey : IEquatable<PublicKey>
    {
        const string RsaSigningAlgorithmName = "SHA-256withRSA";
        const string EcSigningAlgorithmName = "SHA-256withECDSA";
        const string Ed25519SigningAlgorithmName = "Ed25519";

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

        public static PublicKey Deserialize(Stream s) 
            => ProtoBuf.Serializer.Deserialize<PublicKey>(s);

        /// <summary>
        /// Deserialize the public key according to
        /// https://github.com/libp2p/specs/blob/master/peer-ids/peer-ids.md
        /// </summary>
        /// <param name="data"></param>
        /// <returns>The protobuffered public key</returns>
        public static PublicKey Deserialize(byte[] data)
        {
            using(MemoryStream ms = new MemoryStream(data, false)) 
            {
                return Deserialize(ms);
            }
        }

        /// <summary>
        /// Do the signature verification with this public key
        /// </summary>
        /// <param name="data">The data to be verified</param>
        /// <param name="signature">The signature we're check against</param>
        /// <exception cref="NotImplementedException"></exception>
        /// <exception cref="InvalidDataException">The verification failed</exception>
        public void Verify(byte[] data, byte[] signature)
        {
            AsymmetricKeyParameter publicKey;
            string signingAlgorithmName;

            // Make the key material palatable for BouncyCastle...
            switch(Type)
            {
                case KeyType.RSA:
                    // RSA public keys are encoded as SubjectPublicKeyInfo
                    publicKey = PublicKeyFactory.CreateKey(Data);
                    signingAlgorithmName = RsaSigningAlgorithmName;
                    break;
                case KeyType.Ed25519:
                    {
                        // Edwards curve keys are nekkid 32 bytes, envelope it with the SPKI boilerplate
                        byte[] pkixKeyData = Convert.FromBase64String("MCowBQYDK2VwAyEAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=");
                        Data.CopyTo(pkixKeyData, 12);
                        publicKey = PublicKeyFactory.CreateKey(pkixKeyData);
                        signingAlgorithmName = Ed25519SigningAlgorithmName;
                    }
                    break;
                // Secp256k1 keys are encoded with the "standard Bitcoin EC encoding" (??)
                // EcSigningAlgorithmName for KeyType.Secp256k1

                // ECDSA are encoded as SPKI, too.
                default:
                    throw new NotImplementedException($"Using key {Type.GetType().Name} is not yet supported");
            }

            // ...And do the stuff.
            var signer = SignerUtilities.GetSigner(signingAlgorithmName);
            signer.Init(false, publicKey);
            signer.BlockUpdate(data, 0, data.Length);
            if (!signer.VerifySignature(signature))
                throw new InvalidDataException("Data does not match the signature.");
        }

#if true
        /// <summary>
        /// Do the publickey-to-ID according to
        /// https://github.com/libp2p/specs/blob/master/peer-ids/peer-ids.md#encoding
        /// </summary>
        /// <returns>The ID as MultiHash</returns>
        public MultiHash ToId()
        {
            using (var ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, this);

                // If the length of the serialized bytes <= 42, then we compute the "identity" multihash of 
                // the serialized bytes. The idea here is that if the serialized byte array 
                // is short enough, we can fit it in a multihash verbatim without having to 
                // condense it using a hash function.

                // Really 48, not 42? Doesn't matter, encoded Secp256k1 and Ed25519 are
                // 36 and 37 bytes long, respectively.
                var alg = (ms.Length <= 48) ? "identity" : "sha2-256";

                ms.Position = 0;
                return MultiHash.ComputeHash(ms, alg);
            }
        }

        /// <summary>
        /// Converts the peer ID to its public key, if possible
        /// </summary>
        /// <param name="peerId">The peer Id</param>
        /// <returns>The public key. null if it's not taken as-is in its peer ID</returns>
        public static PublicKey FromId(MultiHash peerId)
        {
            if (!peerId.IsIdentityHash) return null;

            return Deserialize(peerId.Digest);
        }

        public static PublicKey FromId(string peerId) 
            => FromId(new MultiHash(peerId));

#endif
        public static PublicKey Import(AsymmetricKeyParameter publicKey)
        {
            var spki = SubjectPublicKeyInfoFactory
                .CreateSubjectPublicKeyInfo(publicKey)
                .GetDerEncoded();

            KeyType type;
            byte[] bytes;

            if (publicKey is RsaKeyParameters)
            {
                // RSA encoded as SubjectsPublicKeyInfo
                bytes = spki;
                type = KeyType.RSA;
            }
            else if (publicKey is Ed25519PublicKeyParameters)
            {
                // Ed25519 are naked 32 bytes
                bytes = spki[12..];
                type = KeyType.Ed25519;
            }
            else if (publicKey is ECPublicKeyParameters)
            {
                bytes = spki; // FIXME Bitcoin EC encoding, anywhere? (33 bytes)
                type = KeyType.Secp256k1;
            }
            else
                throw new NotSupportedException($"The key type {publicKey.GetType().Name} is not supported.");

            return new PublicKey
            {
                Type = type,
                Data = bytes,
            };
        }

        public static implicit operator PublicKey(string b64string)
            => Deserialize(Convert.FromBase64String(b64string));

        public override bool Equals(object obj) 
            => Equals(obj as PublicKey);

        public bool Equals(PublicKey other)
            => other is not null && Type == other.Type && other.Data.SequenceEqual(Data);

        public override int GetHashCode() 
            => HashCode.Combine(Type, Data);

        public static bool operator ==(PublicKey left, PublicKey right) 
            => EqualityComparer<PublicKey>.Default.Equals(left, right);

        public static bool operator !=(PublicKey left, PublicKey right) 
            => !(left == right);
    }
}
