using System;

namespace Downloader;

/// <summary>
/// Provides data for the DownloadService.DownloadProgressChanged event of a DownloadService.
/// </summary>
public class DownloadStartedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadStartedEventArgs"/> class.
    /// </summary>
    /// <param name="fileName">The name of the file being downloaded.</param>
    /// <param name="totalBytes">The total number of bytes to be received.</param>
    public DownloadStartedEventArgs(string fileName, long totalBytes)
    {
        FileName = fileName;
        TotalBytesToReceive = totalBytes;
    }

    /// <summary>
    /// Gets the total number of bytes in a System.Net.WebClient data download operation.
    /// </summary>
    /// <returns>A System.Int64 value that indicates the number of bytes that will be received.</returns>
    public long TotalBytesToReceive { get; }

    /// <summary>
    /// Gets the name of the file which is being downloaded.
    /// </summary>
    public string FileName { get; }
}