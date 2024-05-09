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

        public PrivateKey ReadDaemonPrivateKey(string repodir = null)
        {
            if(repodir == null)
            {
                string homepath = Environment.GetEnvironmentVariable("USERPROFILE");
                repodir = Path.Combine(homepath, ".ipfs");
            }

            Stream s = File.OpenRead(Path.Combine(repodir, "config"));
            StreamReader sr = new(s);
            string json = sr.ReadToEnd();

            JObject c = JObject.Parse(json);
            JToken identity = c["Identity"];
            string pkString = (string) identity["PrivKey"];

            byte[] keyData = Convert.FromBase64String(pkString);

            return PrivateKey.Deserialize(keyData);
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