namespace Downloader
{
    /// <summary>
    /// Provides data for the DownloadService.DownloadProgressChanged event of a
    /// DownloadService.
    /// </summary>
    public class DownloadProgressChangedEventArgs
    {
        public DownloadProgressChangedEventArgs(long totalBytesToReceive, long bytesReceived, long bytesPerSecond)
        {
            TotalBytesToReceive = totalBytesToReceive;
            BytesReceived = bytesReceived;
            ProgressPercentage = (double)bytesReceived * 100 / totalBytesToReceive;
            BytesPerSecondSpeed = bytesPerSecond;
        }

        /// <summary>
        /// Gets the asynchronous task progress percentage.
        /// </summary>
        /// <returns>A percentage value indicating the asynchronous task progress.</returns>
        public double ProgressPercentage { get; }

        /// <summary>
        /// Gets the number of bytes received.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes received.</returns>
        public long BytesReceived { get; }

        /// <summary>
        /// Gets the total number of bytes in a System.Net.WebClient data download operation.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes that will be received.</returns>
        public long TotalBytesToReceive { get; }

        /// <summary>
        /// How many bytes downloaded per second (BPS)
        /// </summary>
        public long BytesPerSecondSpeed { get; }
    }
}
