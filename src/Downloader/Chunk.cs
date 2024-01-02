using System;

namespace Downloader;

/// <summary>
/// Chunk data structure
/// </summary>
public class Chunk
{
    /// <summary>
    /// Chunk unique identity name
    /// </summary>
    public string Id { get; set; }
    
    /// <summary>
    /// Start offset of the chunk in the file bytes
    /// </summary>
    public long Start { get; set; }

    /// <summary>
    /// End offset of the chunk in the file bytes
    /// </summary>
    public long End { get; set; }

    /// <summary>
    /// Current write offset of the chunk
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// How many times to try again after the error
    /// </summary>
    public int MaxTryAgainOnFailover { get; set; }

    /// <summary>
    /// How many milliseconds to wait for a response from the server?
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// How many times has downloading the chunk failed?
    /// </summary>
    public int FailoverCount { get; private set; }

    /// <summary>
    /// Length of current chunk. 
    /// When the chunk length is zero, the file is open to receive new bytes
    /// until no more bytes are received from the server.
    /// </summary>
    public long Length => End - Start + 1;

    /// <summary>
    /// Unused length of current chunk.
    /// When the chunk length is zero, the file is open to receive new bytes
    /// until no more bytes are received from the server.
    /// </summary>
    public long EmptyLength => Length > 0 ? Length - Position : long.MaxValue;

    /// <summary>
    /// Can write more data on this chunk according to the chunk situations?
    /// </summary>
    public bool CanWrite => Length > 0 ? Start + Position < End : true;


    public Chunk()
    {
        Timeout = 1000;
        Id = Guid.NewGuid().ToString("N");
    }

    public Chunk(long start, long end) : this()
    {
        Start = start;
        End = end;
    }

    public bool CanTryAgainOnFailover()
    {
        return FailoverCount++ < MaxTryAgainOnFailover;
    }

    public void Clear()
    {
        Position = 0;
        FailoverCount = 0;
    }

    public bool IsDownloadCompleted()
    {
        var isNoneEmptyFile = Length > 0;
        var isChunkedFilledWithBytes = Start + Position >= End;

        return isNoneEmptyFile && isChunkedFilledWithBytes;
    }

    public bool IsValidPosition()
    {
        return Length == 0 || (Position >= 0 && Position <= Length);
    }
}