using Ipfs.ExtendedApi;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public class FileSystemExApi : IFileSystemExApi
    {
        private IpfsClientEx ipfs;

        internal FileSystemExApi(IpfsClientEx ipfsClientEx)
        {
            this.ipfs = ipfsClientEx;
        }

        // Same as in FileSystemApi, but it's tightly locked away.
        public async Task<FileSystemNode> CreateDirectoryAsync(IEnumerable<IFileSystemLink> links, CancellationToken cancel)
        {
            List<JToken> linkList = new();

            void AddFileLink(IFileSystemLink node)
            {
                JToken newLink = JToken.Parse(@"{ ""Hash"": { ""/"": """" }, ""Name"": """", ""Tsize"": 0 }");
                newLink["Hash"]["/"] = node.Id.ToString();
                newLink["Name"] = node.Name;
                newLink["Tsize"] = node.Size;

                linkList.Add(newLink);
            }

            JToken dir = JToken.Parse(@"{ ""Data"": { ""/"": { ""bytes"": ""CAE"" } }, ""Links"": [] }");
            foreach (var link in links)
                AddFileLink(link);

            // Sorting? I've checked. kubo sorts the links on its own.
            dir["Links"] = JToken.FromObject(linkList);
            var id = await ipfs.Dag.PutAsync(dir, "dag-pb", cancel: cancel).ConfigureAwait(false);

            // HACK: Retrieve the resulting serialized DAG node rather than serializing it itself.
            byte[] rawDAG = await ipfs.Block.GetAsync(id).ConfigureAwait(false);
            long totalBytes = rawDAG.LongLength;

            foreach (IFileSystemLink link in links)
                totalBytes += link.Size;

            return new FileSystemNode
            {
                Id = id,
                Links = links,
                Size = totalBytes,
                IsDirectory = true
            };
        }
    }

    public partial class IpfsClientEx : IpfsClient
    {
        public IpfsClientEx() : base() 
        { 
            InitEx();
        }

        public IpfsClientEx(string host) : base(host) 
        { 
            InitEx();
        }

        private void InitEx() 
        {
            Routing = new RoutingApi(this);
            NameEx = new NameExApi(this);
            FileSystemEx = new FileSystemExApi(this);
        }

        public IRoutingApi Routing { get; private set; }

        public INameExApi NameEx { get; private set; }

        public IFileSystemExApi FileSystemEx { get; private set; }
    }
}