using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public interface INameExApi
    {
        public Task<NamedContent> PublishAsync(string path,
            bool resolve = true, 
            string key = "self", 
            TimeSpan? lifetime = null,
            TimeSpan? ttl = null,
            CancellationToken cancel = default(CancellationToken));

    }
}