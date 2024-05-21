using Ipfs.Cryptography;
using Ipfs.Cryptography.Proto;
using Ipfs.ExtendedApi;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public partial class IpfsClientEx : IpfsClient, IDaemonApi
    {
        private IpfsClientEx ipfs => this;

        public static MultiAddress ReadDaemonAPIAddress(string repodir = null)
        {
            JObject c = ReadConfigFile(ref repodir);
            JToken addresses = c["Addresses"];
            string apiAddress = (string)addresses["API"];

            return new MultiAddress(apiAddress);
        }

        public static PrivateKey ReadDaemonPrivateKey(string repodir = null)
        {
            JObject c = ReadConfigFile(ref repodir);
            JToken identity = c["Identity"];
            string pkString = (string)identity["PrivKey"];

            byte[] keyData = Convert.FromBase64String(pkString);

            return PrivateKey.Deserialize(keyData);
        }

        private static JObject ReadConfigFile(ref string repodir)
        {
            if (repodir == null)
            {
                string homepath = Environment.GetEnvironmentVariable("USERPROFILE");
                repodir = Path.Combine(homepath, ".ipfs");
            }

            using Stream s = File.OpenRead(Path.Combine(repodir, "config"));
            using StreamReader sr = new(s);
            string json = sr.ReadToEnd();

            JObject c = JObject.Parse(json);
            return c;
        }

        public async Task VerifyDaemonAsync(PrivateKey privateKey, CancellationToken cancel = default)
        {
            Peer self = await ipfs.IdAsync(cancel: cancel);

            KeyPair kp = KeyPair.Import(privateKey);
            PublicKey publicKey = kp;

            if (publicKey.ToId() != self.Id)
                throw new InvalidDataException("Daemon doen't match this private key");
        }
    }
}