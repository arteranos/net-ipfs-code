﻿using Ipfs.CoreApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_STANDALONE
using Unity.Net.Http;
#else
using System.Net.Http;
#endif

namespace Ipfs.Http
{
    /// <summary>
    ///   A client that allows access to the InterPlanetary File System (IPFS).
    /// </summary>
    /// <remarks>
    ///   The API is based on the <see href="https://ipfs.io/docs/commands/">IPFS commands</see>.
    /// </remarks>
    /// <seealso href="https://ipfs.io/docs/api/">IPFS API</seealso>
    /// <seealso href="https://ipfs.io/docs/commands/">IPFS commands</seealso>
    /// <remarks>
    ///   <b>IpfsClient</b> is thread safe, only one instance is required by the application.
    /// </remarks>
    public partial class IpfsClient : ICoreApi
    {
        const string unknownFilename = "unknown";

        static object safe = new object();
        static HttpClient api = null;

        /// <summary>
        ///   The default URL to the IPFS HTTP API server.
        /// </summary>
        /// <value>
        ///   The default is "http://localhost:5001".
        /// </value>
        /// <remarks>
        ///   The environment variable "IpfsHttpApi" overrides this value.
        /// </remarks>
        public static Uri DefaultApiUri = new Uri(
            Environment.GetEnvironmentVariable("IpfsHttpApi")
            ?? "http://localhost:5001");

        /// <summary>
        ///   Creates a new instance of the <see cref="IpfsClient"/> class and sets the
        ///   default values.
        /// </summary>
        /// <remarks>
        ///   All methods of IpfsClient are thread safe.  Typically, only one instance is required for
        ///   an application.
        /// </remarks>
        public IpfsClient()
        {
            ApiUri = DefaultApiUri;

            var assembly = typeof(IpfsClient).GetTypeInfo().Assembly;
            var version = assembly.GetName().Version;

            UserAgent = string.Format("{0}/{1}.{2}.{3}", assembly.GetName().Name, version.Major, version.Minor, version.Revision);
            TrustedPeers = new TrustedPeerCollection(this);

            Bootstrap = new BootstrapApi(this);
            Bitswap = new BitswapApi(this);
            Block = new BlockApi(this);
            BlockRepository = new BlockRepositoryApi(this);
            Config = new ConfigApi(this);
            Pin = new PinApi(this);
            Dht = new DhtApi(this);
            Swarm = new SwarmApi(this);
            Dag = new DagApi(this);
            Object = new ObjectApi(this);
            FileSystem = new FileSystemApi(this);
            Mfs = new MfsApi(this);
            PubSub = new PubSubApi(this);
            Key = new KeyApi(this);
            Generic = this;
            Name = new NameApi(this);
            Dns = new DnsApi(this);
            Stats = new StatApi(this);
        }

        /// <summary>
        ///   Creates a new instance of the <see cref="IpfsClient"/> class and specifies
        ///   the <see cref="ApiUri">API host's URL</see>.
        ///   default values
        /// </summary>
        /// <param name="host">
        ///   The URL of the API host.  For example "http://localhost:5001" or "http://ipv4.fiddler:5001".
        /// </param>
        public IpfsClient(string host)
            : this()
        {
            ApiUri = new Uri(host);
        }

        /// <summary>
        ///   The URL to the IPFS API server.  The default is "http://localhost:5001".
        /// </summary>
        public Uri ApiUri { get; set; }

        /// <summary>
        ///   The value of HTTP User-Agent header sent to the API server. 
        /// </summary>
        /// <value>
        ///   The default value is "net-ipfs/M.N", where M is the major and N is minor version
        ///   numbers of the assembly.
        /// </value>
        public string UserAgent { get; set; }

        /// <summary>
        ///   The list of peers that are initially trusted by IPFS.
        /// </summary>
        /// <remarks>
        ///   This is equivalent to <c>ipfs bootstrap list</c>.
        /// </remarks>
        public TrustedPeerCollection TrustedPeers { get; private set; }

        /// <inheritdoc />
        public IBitswapApi Bitswap { get; private set; }

        /// <inheritdoc />
        public IBootstrapApi Bootstrap { get; private set; }

        /// <inheritdoc />
        public IGenericApi Generic { get; private set; }

        /// <inheritdoc />
        public IDnsApi Dns { get; private set; }

        /// <inheritdoc />
        public IStatsApi Stats { get; private set; }

        /// <inheritdoc />
        public INameApi Name { get; private set; }

        /// <inheritdoc />
        public IBlockApi Block { get; private set; }

        /// <inheritdoc />
        public IBlockRepositoryApi BlockRepository { get; private set; }

        /// <inheritdoc />
        public IConfigApi Config { get; private set; }

        /// <inheritdoc />
        public IPinApi Pin { get; private set; }

        /// <inheritdoc />
        public IDagApi Dag { get; private set; }

        /// <inheritdoc />
        public IDhtApi Dht { get; private set; }

        /// <inheritdoc />
        public ISwarmApi Swarm { get; private set; }

        /// <inheritdoc />
        public IObjectApi Object { get; private set; }

        /// <inheritdoc />
        public IFileSystemApi FileSystem { get; private set; }

        /// <inheritdoc />
        public IMfsApi Mfs { get; private set; }

        /// <inheritdoc />
        public IPubSubApi PubSub { get; private set; }

        /// <inheritdoc />
        public IKeyApi Key { get; private set; }

        Uri BuildCommand(string command, string arg = null, params string[] options)
        {
            var url = "/api/v0/" + command;
            var q = new StringBuilder();

            if (arg != null)
            {
                q.Append("&arg=");
                q.Append(WebUtility.UrlEncode(arg));
            }

            foreach (var option in options)
            {
                q.Append('&');
                var i = option.IndexOf('=');
                if (i < 0)
                {
                    q.Append(option);
                }
                else
                {
                    q.Append(option.Substring(0, i));
                    q.Append('=');
                    q.Append(WebUtility.UrlEncode(option.Substring(i + 1)));
                }
            }

            if (q.Length > 0)
            {
                q[0] = '?';
                q.Insert(0, url);
                url = q.ToString();
            }

            return new Uri(ApiUri, url);
        }

        /// <summary>
        ///   Get the IPFS API.
        /// </summary>
        /// <returns>
        ///   A <see cref="HttpClient"/>.
        /// </returns>
        /// <remarks>
        ///   Only one client is needed.  Its thread safe.
        /// </remarks>
        HttpClient Api()
        {
            if (api == null)
            {
                lock (safe)
                {
                    if (api == null)
                    {
                        // Suspected cause of hangs. And, why, when we connect to localhost?
                        // https://github.com/mono/monodevelop/issues/7251
                        // Moreover, Kubo 0.28.0 seems to be more responsive without Automatic Decompression...
                        //if (HttpMessageHandler is HttpClientHandler handler && handler.SupportsAutomaticDecompression)
                        //{
                        //    handler.AutomaticDecompression = DecompressionMethods.GZip
                        //        | DecompressionMethods.Deflate;
                        //}

                        api = new HttpClient(HttpMessageHandler)
                        {
                            Timeout = Timeout.InfiniteTimeSpan
                        };

                        api.DefaultRequestHeaders.Add("User-Agent", UserAgent);
                    }
                }
            }
            return api;
        }

        /// <summary>
        /// The message handler to use for communicating over HTTP.
        /// </summary>
        public HttpMessageHandler HttpMessageHandler { get; set; } = new HttpClientHandler();

        private void EnsureBackgroundThread()
        {
#if ENFORCE_BGTHREAD
            if (!Thread.CurrentThread.IsBackground)
                throw new InvalidOperationException("Running in main thread -- may cause frame drops and deadlocks!");
#endif
        }

        /// <summary>
        ///  Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> returning a string.
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0filels">"file/ls"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="arg">
        ///   The optional argument to the command.
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A string representation of the command's result.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<string> DoCommandAsync(string command, CancellationToken cancel, string arg = null, params string[] options)
        {
            EnsureBackgroundThread();

            var url = BuildCommand(command, arg, options);

            using (var response = await Api().PostAsync(url, null, cancel).ConfigureAwait(false))
            {
                await ThrowOnErrorAsync(response).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return body;
            }
        }

        internal Task DoCommandAsync(Uri url, byte[] bytes, CancellationToken cancel)
        {
            return DoCommandAsync(url, new ByteArrayContent(bytes), cancel);
        }

        internal Task DoCommandAsync(Uri url, Stream stream, CancellationToken cancel)
        {
            return DoCommandAsync(url, new StreamContent(stream), cancel);
        }

        internal Task DoCommandAsync(Uri url, string str, CancellationToken cancel)
        {
            return DoCommandAsync(url, new StringContent(str), cancel);
        }

        internal async Task DoCommandAsync(Uri url, HttpContent content, CancellationToken cancel)
        {
            EnsureBackgroundThread();

            using (var response = await Api().PostAsync(url, new MultipartFormDataContent { { content, "\"file\"" } }, cancel).ConfigureAwait(false))
            {
                await ThrowOnErrorAsync(response).ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }


        /// <summary>
        ///   Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> returning 
        ///   a specific <see cref="Type"/>.
        /// </summary>
        /// <typeparam name="T">
        ///   The <see cref="Type"/> of object to return.
        /// </typeparam>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0filels">"file/ls"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="arg">
        ///   The optional argument to the command.
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A <typeparamref name="T"/>.
        /// </returns>
        /// <remarks>
        ///   The command's response is converted to <typeparamref name="T"/> using
        ///   <c>JsonConvert</c>.
        /// </remarks>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<T> DoCommandAsync<T>(string command, CancellationToken cancel, string arg = null, params string[] options)
        {
            EnsureBackgroundThread();

            var json = await DoCommandAsync(command, cancel, arg, options).ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        ///  Post an <see href="https://ipfs.io/docs/api/">IPFS API command</see> returning a stream.
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0filels">"file/ls"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="arg">
        ///   The optional argument to the command.
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A <see cref="Stream"/> containing the command's result.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<Stream> PostDownloadAsync(string command, CancellationToken cancel, string arg = null, params string[] options)
        {
            EnsureBackgroundThread();

            var url = BuildCommand(command, arg, options);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            var response = await Api().SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);

            await ThrowOnErrorAsync(response).ConfigureAwait(false);

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///  Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> returning a
        ///  <see cref="Stream"/>.
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0filels">"file/ls"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="arg">
        ///   The optional argument to the command.
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A <see cref="Stream"/> containing the command's result.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<Stream> DownloadAsync(string command, CancellationToken cancel, string arg = null, params string[] options)
        {
            EnsureBackgroundThread();

            var url = BuildCommand(command, arg, options);

            var response = await Api().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            await ThrowOnErrorAsync(response).ConfigureAwait(false);

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///  Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> returning a
        ///  a byte array.
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0filels">"file/ls"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="arg">
        ///   The optional argument to the command.
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A byte array containing the command's result.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<byte[]> DownloadBytesAsync(string command, CancellationToken cancel, string arg = null, params string[] options)
        {
            EnsureBackgroundThread();

            var url = BuildCommand(command, arg, options);

            var response = await Api().GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
            await ThrowOnErrorAsync(response).ConfigureAwait(false);

            return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///   Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> that
        ///   requires uploading of a "file".
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0add">"add"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="data">
        ///   A <see cref="Stream"/> containing the data to upload.
        /// </param>
        /// <param name="name">
        ///   The name associated with the <paramref name="data"/>, can be <b>null</b>.
        ///   Typically a filename, such as "hello.txt".
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's value is 
        ///   the HTTP response as a string.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<String> UploadAsync(string command, CancellationToken cancel, Stream data, string name, params string[] options)
        {
            EnsureBackgroundThread();

            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(data);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            if (string.IsNullOrEmpty(name))
                content.Add(streamContent, "file", unknownFilename);
            else
                content.Add(streamContent, "file", name);

            var url = BuildCommand(command, null, options);

            using (var response = await Api().PostAsync(url, content, cancel).ConfigureAwait(false))
            {
                await ThrowOnErrorAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return json;
            }
        }
        /// <summary>
        ///   Perform an <see href="https://ipfs.io/docs/api/">IPFS API command</see> that
        ///   requires uploading of a "file".
        /// </summary>
        /// <param name="command">
        ///   The <see href="https://ipfs.io/docs/api/">IPFS API command</see>, such as
        ///   <see href="https://ipfs.io/docs/api/#apiv0add">"add"</see>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <param name="data">
        ///   A <see cref="Stream"/> containing the data to upload.
        /// </param>
        /// <param name="name">
        ///   The name associated with the <paramref name="data"/>, can be <b>null</b>.
        ///   Typically a filename, such as "hello.txt".
        /// </param>
        /// <param name="options">
        ///   The optional flags to the command.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's value is 
        ///   the HTTP response as a <see cref="Stream"/>.
        /// </returns>
        /// <exception cref="HttpRequestException">
        ///   When the IPFS server indicates an error.
        /// </exception>
        public async Task<Stream> Upload2Async(string command, CancellationToken cancel, Stream data, string name, params string[] options)
        {
            EnsureBackgroundThread();

            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(data);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            content.Add(streamContent, "file", string.IsNullOrEmpty(name) ? unknownFilename : name);

            var url = BuildCommand(command, null, options);

            var response = await Api().PostAsync(url, content, cancel).ConfigureAwait(false);
            await ThrowOnErrorAsync(response).ConfigureAwait(false);

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        public async Task<Stream> UploadMultipleAsync(string command, CancellationToken cancel, string rootDir, List<string> paths, params string[] options)
        {
            EnsureBackgroundThread();

            var content = new MultipartFormDataContent();
            List<Stream> streams = new();

            foreach (string path in paths)
            {
                string fullPath = Path.Combine(rootDir, path); // For file retrieval
                string fileName = path.Replace("\\", "/"); // For file names

                System.Net.Http.HttpContent contentPart = null;
                if (Directory.Exists(fullPath))
                {
                    contentPart = new ByteArrayContent(new byte[0]);
                    contentPart.Headers.ContentType = new MediaTypeHeaderValue("application/x-directory");
                }
                else
                {
                    Stream stream = File.OpenRead(fullPath);
                    streams.Add(stream);
                    contentPart = new StreamContent(stream);
                    //contentPart = new StreamContent(new AsyncLazyBlocking<Stream>(delegate
                    //{ return File.OpenRead(fullPath); }));
                    contentPart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                }

                content.Add(contentPart, "file", fileName);
            }

            var url = BuildCommand(command, null, options);

            HttpResponseMessage response = null;

            try
            {
                response = await Api().PostAsync(url, content, cancel).ConfigureAwait(false);
            }
            finally
            {
                // Need to ~~cross~~ close the streams, whatever it takes.
                foreach (var stream in streams) stream.Dispose();
            }

            await ThrowOnErrorAsync(response).ConfigureAwait(false);

            return await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        /// <summary>
        ///  TODO
        /// </summary>
        public async Task<String> UploadAsync(string command, CancellationToken cancel, byte[] data, params string[] options)
        {
            EnsureBackgroundThread();

            var content = new MultipartFormDataContent();
            var streamContent = new ByteArrayContent(data);

            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(streamContent, "file", unknownFilename);

            var url = BuildCommand(command, null, options);

            using (var response = await Api().PostAsync(url, content, cancel).ConfigureAwait(false))
            {
                await ThrowOnErrorAsync(response).ConfigureAwait(false);

                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return json;
            }
        }

        /// <summary>
        ///   Throws an <see cref="HttpRequestException"/> if the response
        ///   does not indicate success.
        /// </summary>
        /// <param name="response"></param>
        /// <returns>
        ///    <b>true</b>
        /// </returns>
        /// <remarks>
        ///   The API server returns an JSON error in the form <c>{ "Message": "...", "Code": ... }</c>.
        /// </remarks>
        async Task<bool> ThrowOnErrorAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return true;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                var error = "Invalid IPFS command: " + response.RequestMessage.RequestUri;

                throw new HttpRequestException(error);
            }

            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            string message = body;

            try
            {
                var res = JObject.Parse(body);
                message = (string)res["Message"];
            }
            catch { }

            throw new HttpRequestException(message);
        }

        /// <inheritdoc />
        public IAsyncEnumerable<PingResult> Ping(MultiHash peer, int count = 10, CancellationToken cancel = new CancellationToken()) => Generic.Ping(peer, count, cancel);

        /// <inheritdoc />
        public IAsyncEnumerable<PingResult> Ping(MultiAddress address, int count = 10, CancellationToken cancel = new CancellationToken()) => Generic.Ping(address, count, cancel);
    }
}
