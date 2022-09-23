using System;

namespace Downloader;

public class ChunkMergeStartedEventArgs : EventArgs
{
    public ChunkMergeStartedEventArgs(string fileName, long totalBytes)
    {
        TotalBytesToCopy = totalBytes;
        FileName = fileName;
    }

    /// <summary>
    ///     Gets the total number of bytes in the copy operation
    /// </summary>
    /// <returns>An System.Int64 value that indicates the number of bytes that will be copied.</returns>
    public long TotalBytesToCopy { get; internal set; }
    
    /// <summary>
    ///     The name of file which is being merged to
    /// </summary>
    public string FileName { get; }
}