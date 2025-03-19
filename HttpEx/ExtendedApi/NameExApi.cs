using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public static class TimeSpanExtension
    {
        /// <summary>
        ///   Returns a short string representation of the duration in seconds to days
        /// </summary>
        /// <param name="t">The duration</param>
        /// <returns>The duration, like "1m7s" or "1d" </returns>
        public static string ToShortString(this TimeSpan t)
        {
            StringBuilder sb = new();

            if (t < TimeSpan.Zero)
            {
                sb.Append("-");
                t = t.Negate();
            }

            // Kubo API documentation errata: 'd' as 'days' are unsupported
            //if (t.Days > 0)
            //    sb.Append($"{t.Days}d");

            if (t.Hours > 0 || t.Days > 0)
                sb.Append($"{24 * t.Days + t.Hours}h");

            if (t.Minutes > 0)
                sb.Append($"{t.Minutes}m");

            if (t.Seconds > 0)
                sb.Append($"{t.Seconds}s");

            return sb.ToString();
        }
    }
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