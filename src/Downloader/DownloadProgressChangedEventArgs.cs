using System;

namespace Downloader
{
    /// <summary>
    /// Provides data for the DownloadService.DownloadProgressChanged event of a
    /// DownloadService.
    /// </summary>
    public class DownloadProgressChangedEventArgs : EventArgs
    {
        public DownloadProgressChangedEventArgs(string id)
        {
            ProgressId = id ?? "Main";
        }

        /// <summary>
        /// Progress unique identity
        /// </summary>
        public string ProgressId { get; }

        /// <summary>
        /// Gets the asynchronous task progress percentage.
        /// </summary>
        /// <returns>A percentage value indicating the asynchronous task progress.</returns>
        public double ProgressPercentage => (double)BytesReceived * 100 / TotalBytesToReceive;

        /// <summary>
        /// Gets the number of bytes received.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes received.</returns>
        public long BytesReceived { get; set; }

        /// <summary>
        /// Gets the total number of bytes in a System.Net.WebClient data download operation.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes that will be received.</returns>
        public long TotalBytesToReceive { get; set; } = 1;

        /// <summary>
        /// How many bytes downloaded per second (BPS)
        /// </summary>
        public long BytesPerSecondSpeed { get; set; }
        
        /// <summary>
        /// How many bytes progressed per this time
        /// </summary>
        public long ProgressedByteSize { get; set; }
    }
}
