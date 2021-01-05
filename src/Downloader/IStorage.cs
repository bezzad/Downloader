using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    /// <summary>
    ///     Storage Strategy pattern interface
    /// </summary>
    public interface IStorage
    {
        /// <summary>
        ///     Open existing storage for reading
        /// </summary>
        Stream OpenRead();

        /// <summary>
        ///     Asynchronously writes a sequence of bytes to the current storage.
        /// </summary>
        /// <param name="data">The byte array to write data from.</param>
        /// <param name="offset">The zero-based byte offset in data from which to begin copying bytes to the storage.</param>
        /// <param name="count">The maximum number of bytes to write.</param>
        Task WriteAsync(byte[] data, int offset, int count);

        /// <summary>
        ///     Release all resources used by the storage.
        /// </summary>
        void Clear();

        /// <summary>
        ///     Gets the length in bytes of the storage.
        /// </summary>
        long GetLength();
    }
}
