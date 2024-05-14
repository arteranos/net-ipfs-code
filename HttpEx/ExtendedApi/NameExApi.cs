using Ipfs.Unity;
using Newtonsoft.Json.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public class NameExApi : INameExApi
    {
        private IpfsClientEx ipfs;

        internal NameExApi(IpfsClientEx ipfsClientEx)
        {
            this.ipfs = ipfsClientEx;
        }

        public Task<NamedContent> PublishAsync(Cid id, string key = "self", TimeSpan? lifetime = null, TimeSpan? ttl = null, CancellationToken cancel = default)
        {
            return PublishAsync("/ipfs/" + id.Encode(), false, key, lifetime, ttl, cancel);
        }

        public async Task<NamedContent> PublishAsync(string path, bool resolve = true, string key = "self", TimeSpan? lifetime = null, TimeSpan? ttl = null, CancellationToken cancel = default)
        {
            lifetime ??= new TimeSpan(2, 0, 0, 0);
            ttl ??= new TimeSpan(0, 1, 0, 0);

            var json = await ipfs.DoCommandAsync("name/publish", cancel,
                path,
                $"lifetime={lifetime.Value.ToShortString()}",
                $"ttl={ttl.Value.ToShortString()}",
                $"resolve={resolve.ToString().ToLowerInvariant()}",
                $"key={key}");

            var info = JObject.Parse(json);
            return new NamedContent
            {
                NamePath = (string)info["Name"],
                ContentPath = (string)info["Value"]
            };
        }
    }
}