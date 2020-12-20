using System;

namespace Downloader
{
    /// <summary>
    /// Provides data for the DownloadService.DownloadProgressChanged event of a
    /// DownloadService.
    /// </summary>
    public class DownloadStartedEventArgs : EventArgs
    {
        public DownloadStartedEventArgs(string fileName, long totalBytes)
        {
            FileName = fileName;
            TotalBytesToReceive = totalBytes;
        }
        
        /// <summary>
        /// Gets the total number of bytes in a System.Net.WebClient data download operation.
        /// </summary>
        /// <returns>An System.Int64 value that indicates the number of bytes that will be received.</returns>
        public long TotalBytesToReceive { get; set; } = 1;

        /// <summary>
        /// The name of file which is downloading
        /// </summary>
        public string FileName { get; set; }
    }
}
