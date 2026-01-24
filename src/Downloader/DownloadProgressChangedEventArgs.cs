using System;

namespace Downloader;

/// <summary>
/// Provides any information about progress, like progress percentage, speed,
/// total received bytes and received bytes array to live streaming, for the DownloadService.DownloadProgressChanged event of a
/// DownloadService.
/// </summary>
public class DownloadProgressChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadProgressChangedEventArgs"/> class.
    /// </summary>
    /// <param name="id">The unique identity of the progress.</param>
    public DownloadProgressChangedEventArgs(string id)
    {
        ProgressId = id ?? "Main";
    }

    /// <summary>
    /// Gets the progress unique identity.
    /// </summary>
    public string ProgressId { get; }

    /// <summary>
    /// Gets the asynchronous task progress percentage.
    /// </summary>
    /// <returns>A percentage value indicating the asynchronous task progress.</returns>
    public double ProgressPercentage => TotalBytesToReceive == 0 ? 0 : ((double)ReceivedBytesSize * 100) / TotalBytesToReceive;

    /// <summary>
    /// Gets the number of received bytes.
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of received bytes.</returns>
    public long ReceivedBytesSize { get; init; }

    /// <summary>
    /// Gets the total number of bytes in a System.Net.WebClient data download operation.
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of bytes that will be received.</returns>
    public long TotalBytesToReceive { get; init; }

    /// <summary>
    /// Gets the number of bytes downloaded per second.
    /// </summary>
    public double BytesPerSecondSpeed { get; set; }

    /// <summary>
    /// Gets the average download speed.
    /// </summary>
    public double AverageBytesPerSecondSpeed { get; init; }

    /// <summary>
    /// Gets the number of bytes progressed per this time.
    /// </summary>
    public long ProgressedByteSize { get; init; }

    /// <summary>
    /// Gets the received bytes.
    /// This property is filled when the EnableLiveStreaming option is true.
    /// </summary>
    /// <returns>A byte array that indicates the received bytes.</returns>
    public Memory<byte> ReceivedBytes { get; init; }

    /// <summary>
    /// Gets the number of chunks being downloaded currently.
    /// </summary>
    public int ActiveChunks { get; set; }
}