﻿using Ipfs.CoreApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    class FileSystemApi : IFileSystemApi
    {
        private IpfsClient ipfs;
        private Lazy<DagNode> emptyFolder;

        internal FileSystemApi(IpfsClient ipfs)
        {
            this.ipfs = ipfs;
            this.emptyFolder = new Lazy<DagNode>(() => ipfs.Object.NewDirectoryAsync().Result);
        }

        public async Task<IFileSystemNode> AddFileAsync(string path, AddFileOptions options = null, CancellationToken cancel = default)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var node = await AddAsync(stream, Path.GetFileName(path), options, cancel).ConfigureAwait(false);
                return node;
            }
        }

        public Task<IFileSystemNode> AddTextAsync(string text, AddFileOptions options = null, CancellationToken cancel = default)
        {
            return AddAsync(new MemoryStream(Encoding.UTF8.GetBytes(text), false), "", options, cancel);
        }

        public async Task<IFileSystemNode> AddAsync(Stream stream, string name = "", AddFileOptions options = null, CancellationToken cancel = default)
        {
            if (options == null)
                options = new AddFileOptions();

            var opts = new List<string>();
            if (!options.Pin)
                opts.Add("pin=false");
            if (options.Wrap)
                opts.Add("wrap-with-directory=true");
            if (options.RawLeaves)
                opts.Add("raw-leaves=true");
            if (options.OnlyHash)
                opts.Add("only-hash=true");
            if (options.Trickle)
                opts.Add("trickle=true");
            if (options.Progress != null)
                opts.Add("progress=true");
            if (options.Hash != MultiHash.DefaultAlgorithmName)
                opts.Add($"hash=${options.Hash}");
            if (options.Encoding != MultiBase.DefaultAlgorithmName)
                opts.Add($"cid-base=${options.Encoding}");
            if (!string.IsNullOrWhiteSpace(options.ProtectionKey))
                opts.Add($"protect={options.ProtectionKey}");

            opts.Add($"chunker=size-{options.ChunkSize}");

            var response = await ipfs.Upload2Async("add", cancel, stream, name, opts.ToArray()).ConfigureAwait(false);

            // The result is a stream of LDJSON objects.
            // See https://github.com/ipfs/go-ipfs/issues/4852
            FileSystemNode fsn = null;
            using (var sr = new StreamReader(response))
            using (var jr = new JsonTextReader(sr) { SupportMultipleContent = true })
            {
                while (jr.Read())
                {
                    var r = await JObject.LoadAsync(jr, cancel).ConfigureAwait(false);

                    // If a progress report.
                    if (r.ContainsKey("Bytes"))
                    {
                        options.Progress?.Report(new TransferProgress
                        {
                            Name = (string)r["Name"],
                            Bytes = (ulong)r["Bytes"]
                        });
                    }

                    // Else must be an added file.
                    else
                    {
                        fsn = new FileSystemNode
                        {
                            Id = (string)r["Hash"],
                            Size = long.Parse((string)r["Size"]),
                            IsDirectory = false,
                            Name = name,
                        };
                    }
                }
            }

            fsn.IsDirectory = options.Wrap;
            return fsn;
        }

        public async Task<IFileSystemNode> AddDirectoryAsync(string path, bool recursive = true, AddFileOptions options = null, CancellationToken cancel = default)
        {
            if (options == null)
                options = new AddFileOptions();
            options.Wrap = false;

            // Add the files and sub-directories.
            path = Path.GetFullPath(path);
            var files = Directory
                .EnumerateFiles(path)
                .Select(p => AddFileAsync(p, options, cancel));
            if (recursive)
            {
                var folders = Directory
                    .EnumerateDirectories(path)
                    .Select(dir => AddDirectoryAsync(dir, recursive, options, cancel));
                files = files.Union(folders);
            }

            // go-ipfs v0.4.14 sometimes fails when sending lots of 'add file'
            // requests.  It's happy with adding one file at a time.
#if true
            var links = new List<IFileSystemLink>();
            foreach (var file in files)
            {
                var node = await file;
                links.Add(node.ToLink());
            }
#else
            var nodes = await Task.WhenAll(files);
            var links = nodes.Select(node => node.ToLink());
#endif
            // Create the directory with links to the created files and sub-directories
            FileSystemNode fsn = await CreateDirectoryAsync(links, options, cancel);

            fsn.Name = Path.GetFileName(path);

            return fsn;
        }

        async Task<FileSystemNode> CreateDirectoryAsync(IEnumerable<IFileSystemLink> links, AddFileOptions options, CancellationToken cancel)
        {
            void AddFileLink(List<JToken> linkList, IFileSystemLink node)
            {
                JToken newLink = JToken.Parse(@"{ ""Hash"": { ""/"": """" }, ""Name"": """", ""Tsize"": 0 }");
                newLink["Hash"]["/"] = node.Id.ToString();
                newLink["Name"] = node.Name;
                newLink["Tsize"] = node.Size;

                linkList.Add(newLink);
            }

            JToken dir = JToken.Parse(@"{ ""Data"": { ""/"": { ""bytes"": ""CAE"" } }, ""Links"": [] }");
            List<JToken> linkList = new();
            foreach (var link in links)
                AddFileLink(linkList, link);

            dir["Links"] = JToken.FromObject(linkList);
            var id = await ipfs.Dag.PutAsync(dir, "dag-pb").ConfigureAwait(false);

            return new FileSystemNode
            {
                Id = id,
                Links = links,
                IsDirectory = true
            };
        }


        /// <summary>
        ///   Reads the content of an existing IPFS file as text.
        /// </summary>
        /// <param name="path">
        ///   A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
        ///   or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   The contents of the <paramref name="path"/> as a <see cref="string"/>.
        /// </returns>
        public async Task<String> ReadAllTextAsync(string path, CancellationToken cancel = default)
        {
            using (var data = await ReadFileAsync(path, cancel).ConfigureAwait(false))
            using (var text = new StreamReader(data))
            {
                return await text.ReadToEndAsync();
            }
        }


        /// <summary>
        ///   Opens an existing IPFS file for reading.
        /// </summary>
        /// <param name="path">
        ///   A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
        ///   or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A <see cref="Stream"/> to the file contents.
        /// </returns>
        public Task<Stream> ReadFileAsync(string path, CancellationToken cancel = default)
        {
            return ipfs.PostDownloadAsync("cat", cancel, path);
        }

        public Task<Stream> ReadFileAsync(string path, long offset, long length = 0, CancellationToken cancel = default)
        {
            // https://github.com/ipfs/go-ipfs/issues/5380
            if (offset > int.MaxValue)
                throw new NotSupportedException("Only int offsets are currently supported.");
            if (length > int.MaxValue)
                throw new NotSupportedException("Only int lengths are currently supported.");

            if (length == 0)
                length = int.MaxValue; // go-ipfs only accepts int lengths
            return ipfs.PostDownloadAsync("cat", cancel, path,
                $"offset={offset}",
                $"length={length}");
        }

        /// <inheritdoc cref="ListAsync"/>
        public Task<IFileSystemNode> ListFileAsync(string path, CancellationToken cancel = default)
        {
            return ListAsync(path, cancel);
        }

        /// <summary>
        ///   Get information about the directory.
        /// </summary>
        /// <param name="path">
        ///   A path to an existing directory, such as "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task. When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns></returns>
        public async Task<IFileSystemNode> ListAsync(string path, CancellationToken cancel = default)
        {
            var json = await ipfs.DoCommandAsync("ls", cancel, path).ConfigureAwait(false);
            var r = JObject.Parse(json);
            var o = (JObject)r["Objects"]?[0];

            var node = new FileSystemNode()
            {
                Id = (string)o["Hash"],
                IsDirectory = true,
                Links = Array.Empty<FileSystemLink>(),
            };

            if (o["Links"] is JArray links)
            {
                node.Links = links
                    .Select(l => new FileSystemLink()
                    {
                        Name = (string)l["Name"],
                        Id = (string)l["Hash"],
                        Size = (long)l["Size"],
                    })
                    .ToArray();
            }

            return node;
        }

        public Task<Stream> GetAsync(string path, bool compress = false, CancellationToken cancel = default)
        {
            return ipfs.PostDownloadAsync("get", cancel, path, $"compress={compress}");
        }
    }
}
