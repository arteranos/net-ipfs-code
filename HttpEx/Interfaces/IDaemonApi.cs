using Ipfs.Cryptography.Proto;
using System.Threading;
using System.Threading.Tasks;

namespace Ipfs.ExtendedApi
{
    /// <summary>
    ///   Methods to manage the daemon, not IPFS itself
    /// </summary>
    public interface IDaemonApi
    {
        /// <summary>
        ///   Verify the daemon wether if it's ID matches to its private key
        ///   (e.g. replacing the daemon with a poisoned one)
        /// </summary>
        /// <param name="privateKey">
        ///   The <see cref="PrivateKey"/> the daemon's private key
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task. When cancelled, the <see cref="TaskCanceledException"/> is NOT raised.
        /// </param>
        /// <returns>Throws an exception if the peer ID wouldn't match</returns>
        Task VerifyDaemonAsync(PrivateKey privateKey, CancellationToken cancel = default(CancellationToken));
    }
}