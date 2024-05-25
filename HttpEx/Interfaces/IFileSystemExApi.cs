using Ipfs.CoreApi;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.Http
{
    public interface IFileSystemExApi
    {
        /// <summary>
        /// Synthesize a directory out of a collection of <see cref="IFileSystemLink"/>s
        /// </summary>
        /// <param name="links"></param>
        /// <param name="cancel"></param>
        /// <returns>The resulting <see cref="FileSystemNode"/></returns>
        public Task<FileSystemNode> CreateDirectoryAsync(IEnumerable<IFileSystemLink> links, CancellationToken cancel = default);

    }
}