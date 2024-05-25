using Ipfs.CoreApi;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    class BlockApi : IBlockApi
    {
        IpfsClient ipfs;

        internal BlockApi(IpfsClient ipfs)
        {
            this.ipfs = ipfs;
        }

        public async Task<byte[]> GetAsync(Cid id, CancellationToken cancel = default(CancellationToken))
        {
            using Stream s = await ipfs.PostDownloadAsync("block/get", cancel, id);
            using MemoryStream ms = new();
            byte[] buffer = new byte[16 * 1024];

            while (true)
            {
                int n = s.Read(buffer, 0, buffer.Length);
                if (n <= 0) break;
                ms.Write(buffer, 0, n);
            }
            ms.Position = 0;
            return ms.ToArray();
        }

        public async Task<Cid> PutAsync(
            byte[] data,
            string contentType = Cid.DefaultContentType,
            string multiHash = MultiHash.DefaultAlgorithmName,
            string encoding = MultiBase.DefaultAlgorithmName,
            bool pin = false,
            CancellationToken cancel = default(CancellationToken))
        {
            var options = new List<string>();
            if (multiHash != MultiHash.DefaultAlgorithmName ||
                contentType != Cid.DefaultContentType ||
                encoding != MultiBase.DefaultAlgorithmName)
            {
                options.Add($"mhtype={multiHash}");
                options.Add($"format={contentType}");
                options.Add($"cid-base={encoding}");
            }
            var json = await ipfs.UploadAsync("block/put", cancel, data, options.ToArray()).ConfigureAwait(false);
            var info = JObject.Parse(json);
            Cid cid = (string)info["Key"];

            if (pin)
            {
                await ipfs.Pin.AddAsync(cid, recursive: false, cancel: cancel);
            }

            return cid;
        }

        public async Task<Cid> PutAsync(
            Stream data,
            string contentType = Cid.DefaultContentType,
            string multiHash = MultiHash.DefaultAlgorithmName,
            string encoding = MultiBase.DefaultAlgorithmName,
            bool pin = false,
            CancellationToken cancel = default(CancellationToken))
        {
            var options = new List<string>();
            if (multiHash != MultiHash.DefaultAlgorithmName ||
                contentType != Cid.DefaultContentType ||
                encoding != MultiBase.DefaultAlgorithmName)
            {
                options.Add($"mhtype={multiHash}");
                options.Add($"format={contentType}");
                options.Add($"cid-base={encoding}");
            }
            var json = await ipfs.UploadAsync("block/put", cancel, data, null, options.ToArray()).ConfigureAwait(false);
            var info = JObject.Parse(json);
            Cid cid = (string)info["Key"];

            if (pin)
            {
                await ipfs.Pin.AddAsync(cid, recursive: false, cancel: cancel);
            }

            return cid;
        }

        public async Task<IDataBlock> StatAsync(Cid id, CancellationToken cancel = default(CancellationToken))
        {
            var json = await ipfs.DoCommandAsync("block/stat", cancel, id).ConfigureAwait(false);
            var info = JObject.Parse(json);
            return new Block
            {
                Size = (long)info["Size"],
                Id = (string)info["Key"]
            };
        }

        public async Task<Cid> RemoveAsync(Cid id, bool ignoreNonexistent = false, CancellationToken cancel = default(CancellationToken))
        {
            var json = await ipfs.DoCommandAsync("block/rm", cancel, id, "force=" + ignoreNonexistent.ToString().ToLowerInvariant()).ConfigureAwait(false);
            if (json.Length == 0)
                return null;
            var result = JObject.Parse(json);
            var error = (string)result["Error"];
            if (error != null)
                throw new HttpRequestException(error);
            return (Cid)(string)result["Hash"];
        }

    }

}
