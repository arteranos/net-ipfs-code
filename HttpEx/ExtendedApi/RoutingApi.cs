using Ipfs.ExtendedApi;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
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

        public async Task<IEnumerable<(IPAddress, ProtocolType, int)>> FindPeerAddressesAsync(MultiHash id, CancellationToken cancel = default)
        {
            Peer peer = await ipfs.IdAsync(id, cancel);

            HashSet<(IPAddress, ProtocolType, int)> addresses = new();
            foreach (MultiAddress multiAddress in peer.Addresses)
            {
                string[] parts = multiAddress.ToString().Split('/');
                if (parts.Length < 2) continue;
                if (parts[0] != string.Empty) continue;

                IPAddress addr = IPAddress.None;
                int port = -1;
                ProtocolType type = ProtocolType.Unknown;

                for (int i = 1;  i < parts.Length; i++) 
                {
                    switch (parts[i])
                    {
                        case "ip4":
                        case "ip6":
                            i++;
                            addr = IPAddress.Parse(parts[i]); break;
                        case "tcp":
                            i++;
                            type = ProtocolType.Tcp;
                            port = int.Parse(parts[i]); break;
                        case "udp":
                            i++;
                            type = ProtocolType.Udp;
                            port = int.Parse(parts[i]); break;
                        case "p2p":
                        case "ipfs":
                            i++;
                            break;
                        default: // Likely p2p-circuit, devaluing the information we got so far.
                            addr = IPAddress.None;
                            type = ProtocolType.Unknown;
                            continue;
                    }
                }

                if (addr != IPAddress.None && type != ProtocolType.Unknown)
                    addresses.Add((addr, type,port));
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