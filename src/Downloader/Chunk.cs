using System;

namespace Downloader;

/// <summary>
/// Represents a chunk of data in a file download operation.
/// </summary>
public class Chunk
{
    /// <summary>
    /// Gets or sets the unique identifier for the chunk.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the start offset of the chunk in the file bytes.
    /// </summary>
    public long Start { get; set; }

    /// <summary>
    /// Gets or sets the end offset of the chunk in the file bytes.
    /// </summary>
    public long End { get; set; }

    /// <summary>
    /// Gets or sets the current write offset of the chunk.
    /// </summary>
    public long Position { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of times to try again after an error.
    /// </summary>
    public int MaxTryAgainOnFailure { get; set; }

    /// <summary>
    /// Gets or sets the timeout in milliseconds to wait for a response from the server.
    /// </summary>
    public int Timeout { get; set; }

    /// <summary>
    /// Gets the number of times downloading the chunk has failed.
    /// </summary>
    public int FailureCount { get; private set; }

    /// <summary>
    /// Gets the length of the current chunk.
    /// When the chunk length is zero, the file is open to receive new bytes
    /// until no more bytes are received from the server.
    /// </summary>
    public long Length => End - Start + 1;

    /// <summary>
    /// Gets the unused length of the current chunk.
    /// When the chunk length is zero, the file is open to receive new bytes
    /// until no more bytes are received from the server.
    /// </summary>
    public long EmptyLength => Length > 0 ? Length - Position : long.MaxValue;

    /// <summary>
    /// Gets a value indicating whether more data can be written to this chunk according to the chunk's situation.
    /// </summary>
    public bool CanWrite => Length <= 0 || Start + Position < End;

    /// <summary>
    /// Initializes a new instance of the <see cref="Chunk"/> class with default values.
    /// </summary>
    public Chunk()
    {
        Timeout = 1000;
        Id = Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Chunk"/> class with the specified start and end positions.
    /// </summary>
    /// <param name="start">The start offset of the chunk in the file bytes.</param>
    /// <param name="end">The end offset of the chunk in the file bytes.</param>
    public Chunk(long start, long end) : this()
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Determines whether the chunk can be retried on failure.
    /// </summary>
    /// <returns>True if the chunk can be retried; otherwise, false.</returns>
    public bool CanTryAgainOnFailure()
    {
        return FailureCount++ < MaxTryAgainOnFailure;
    }

    /// <summary>
    /// Clears the chunk's position and failure count.
    /// </summary>
    public void Clear()
    {
        Position = 0;
        FailureCount = 0;
    }

    /// <summary>
    /// Determines whether the download of the chunk is completed.
    /// </summary>
    /// <returns>True if the download is completed; otherwise, false.</returns>
    public bool IsDownloadCompleted()
    {
        bool isNoneEmptyFile = Length > 0;
        bool isChunkedFilledWithBytes = Start + Position >= End;

        return isNoneEmptyFile && isChunkedFilledWithBytes;
    }

    /// <summary>
    /// Determines whether the current position of the chunk is valid.
    /// </summary>
    /// <returns>True if the position is valid; otherwise, false.</returns>
    public bool IsValidPosition()
    {
        return Length == 0 || (Position >= 0 && Position <= Length);
    }
}