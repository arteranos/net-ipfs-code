using ProtoBuf;

namespace Ipfs.Cryptography.Proto
{
    public enum KeyType
    {
        RSA = 0,
        Ed25519 = 1,
        Secp256k1 = 2,
        ECDH = 4,
    }

    [ProtoContract]
    public partial class PublicKey
    {
        [ProtoMember(1, IsRequired = true)]
        public KeyType Type;
        [ProtoMember(2, IsRequired = true)]
        public byte[] Data;
    }

    [ProtoContract]
    public partial class PrivateKey
    {
        [ProtoMember(1, IsRequired = true)]
        public KeyType Type;
        [ProtoMember(2, IsRequired = true)]
        public byte[] Data;
    }
}
