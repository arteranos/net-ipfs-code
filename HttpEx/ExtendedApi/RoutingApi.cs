using Ipfs.ExtendedApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public class RoutingApi : IRoutingApi
    {
        private IpfsClientEx ipfs;

        internal RoutingApi(IpfsClientEx ipfsClientEx)
        {
            this.ipfs = ipfsClientEx;
        }

        public async Task<IEnumerable<IPAddress>> FindPeerAddressesAsync(MultiHash id, CancellationToken cancel = default)
        {
            Peer peer = await ipfs.IdAsync(id, cancel);

            HashSet<IPAddress> addresses = new();
            foreach (MultiAddress multiAddress in peer.Addresses)
            {
                string[] parts = multiAddress.ToString().Split('/');
                if (parts.Length < 2) continue;
                if (parts[0] != string.Empty) continue;

                IPAddress addr;
                switch (parts[1])
                {
                    case "ip4":
                    case "ip6":
                        addr = IPAddress.Parse(parts[2]); break;
                    default:
                        continue;
                }

                addresses.Add(addr);
            }
            return addresses;
        }

        public Task<Peer> FindPeerAsync(MultiHash id, CancellationToken cancel = default)
        {
            return ipfs.IdAsync(id, cancel);
        }

        public async Task<IEnumerable<Peer>> FindProvidersAsync(Cid id, int limit = 20, Action<Peer> providerFound = null, CancellationToken cancel = default)
        {
            var stream = await ipfs.PostDownloadAsync("routing/findprovs", cancel, id, $"num-providers={limit}");
            return ProviderFromStream(stream, providerFound, limit);
        }

        IEnumerable<Peer> ProviderFromStream(Stream stream, Action<Peer> providerFound, int limit = int.MaxValue)
        {
            using (var sr = new StreamReader(stream))
            {
                var n = 0;
                while (!sr.EndOfStream && n < limit)
                {
                    var json = sr.ReadLine();

                    var r = JObject.Parse(json);

                    // Only direct provision, not by referral.
                    var entryType = (int)r["Type"];
                    if (entryType != 4) continue;

                    var id = (string)r["ID"];
                    if (id != String.Empty)
                    {
                        ++n;
                        Peer peer = new Peer { Id = new MultiHash(id) };
                        providerFound?.Invoke(peer);
                        yield return peer;
                    }
                    else
                    {
                        var responses = (JArray)r["Responses"];
                        if (responses != null)
                        {
                            foreach (var response in responses)
                            {
                                var rid = (string)response["ID"];
                                if (rid != String.Empty)
                                {
                                    ++n;
                                    Peer peer = new Peer { Id = new MultiHash(rid) };
                                    providerFound?.Invoke(peer);
                                    yield return peer;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}