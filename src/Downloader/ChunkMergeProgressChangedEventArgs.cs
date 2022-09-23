using System;

namespace Downloader;

public class ChunkMergeProgressChangedEventArgs : EventArgs
{
    public ChunkMergeProgressChangedEventArgs(string id)
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
    public double ProgressPercentage => TotalBytesToCopy == 0 ? 0 : ((double)TotalCopiedBytesSize * 100) / TotalBytesToCopy;
    
    /// <summary>
    ///     The size of the chunk we are currently working on
    /// </summary>
    public double ChunkSize { get; internal set; }
    
    /// <summary>
    ///     Gets the progress of the copy of the current chunk
    /// </summary>
    /// <returns>A percentage value indicating the asynchronous task progress.</returns>
    public double ChunkProgressPercentage => ChunkSize == 0 ? 0 : ((double)ChunkCopiedBytesSize * 100) / ChunkSize;

    /// <summary>
    ///     Gets the number of copied bytes.
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of received bytes.</returns>
    public long TotalCopiedBytesSize { get; internal set; }
    
    /// <summary>
    ///     Gets the number of bytes copied in current chunk.
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of received bytes.</returns>
    public long ChunkCopiedBytesSize { get; internal set; }

    /// <summary>
    ///     Gets the total number of bytes in the copy operation
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of bytes that will be copied.</returns>
    public long TotalBytesToCopy { get; internal set; }

    /// <summary>
    ///     How many bytes copied per second
    /// </summary>
    public double BytesPerSecondSpeed { get; internal set; }

    /// <summary>
    ///     Average copy speed
    /// </summary>
    public double AverageBytesPerSecondSpeed { get; internal set; }

    /// <summary>
    ///     How many bytes progressed per this time
    /// </summary>
    public long ProgressedByteSize { get; internal set; }
}