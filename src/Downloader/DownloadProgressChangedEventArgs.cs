using System;

namespace Downloader
{
    /// <summary>
    ///     Provides data for the DownloadService.DownloadProgressChanged event of a
    ///     DownloadService.
    /// </summary>
    public class DownloadProgressChangedEventArgs : EventArgs
    {
        public DownloadProgressChangedEventArgs(string id)
        {
            ProgressId = id ?? "Main";
        }

        /// <summary>
        ///     Progress unique identity
        /// </summary>
        public string ProgressId { get; }

        /// <summary>
        ///     Gets the asynchronous task progress percentage.
        /// </summary>
        /// <returns>A percentage value indicating the asynchronous task progress.</returns>
        public double ProgressPercentage => TotalBytesToReceive == 0 ? 0 : ((double)ReceivedBytesSize * 100) / TotalBytesToReceive;

        /// <summary>
        ///     Gets the number of received bytes.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of received bytes.</returns>
        public long ReceivedBytesSize { get; set; }

        /// <summary>
        ///     Gets the total number of bytes in a System.Net.WebClient data download operation.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes that will be received.</returns>
        public long TotalBytesToReceive { get; set; }

        /// <summary>
        ///     How many bytes downloaded per second
        /// </summary>
        public double BytesPerSecondSpeed { get; set; }

        /// <summary>
        ///     Average download speed
        /// </summary>
        public double AverageBytesPerSecondSpeed { get; set; }

        /// <summary>
        ///     How many bytes progressed per this time
        /// </summary>
        public long ProgressedByteSize { get; set; }

        /// <summary>
        ///     Gets the received bytes.
        /// </summary>
        /// <returns>A byte array that indicates the received bytes.</returns>
        public byte[] ReceivedBytes { get; set; }
    }
}