using System.Linq;

namespace Downloader;

/// <summary>
/// Manages the creation and validation of chunks for a download package.
/// </summary>
public class ChunkHub
{
    private readonly DownloadConfiguration _config;
    private int _chunkCount;
    private long _chunkSize;
    private long _startOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkHub"/> class with the specified configuration.
    /// </summary>
    /// <param name="config">The download configuration.</param>
    public ChunkHub(DownloadConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Sets the file chunks for the specified download package.
    /// </summary>
    /// <param name="package">The download package to set the chunks for.</param>
    public void SetFileChunks(DownloadPackage package)
    {
        Validate(package);
        if (package.Chunks is null)
        {
            package.Chunks = new Chunk[_chunkCount];
            for (int i = 0; i < _chunkCount; i++)
            {
                long startPosition = _startOffset + (i * _chunkSize);
                long endPosition = startPosition + _chunkSize - 1;
                package.Chunks[i] = GetChunk(i.ToString(), startPosition, endPosition);
            }
            package.Chunks.Last().End += package.TotalFileSize % _chunkCount; // add remaining bytes to last chunk
        }
        else
        {
            package.Validate();
        }
    }

    /// <summary>
    /// Validates the download package and sets the chunk count, chunk size, and start offset.
    /// </summary>
    /// <param name="package">The download package to validate.</param>
    private void Validate(DownloadPackage package)
    {
        _chunkCount = _config.ChunkCount;
        _startOffset = _config.RangeLow;

        if (_startOffset < 0)
        {
            _startOffset = 0;
        }

        if (package.TotalFileSize < _chunkCount)
        {
            _chunkCount = (int)package.TotalFileSize;
        }

        if (_chunkCount < 1)
        {
            _chunkCount = 1;
        }

        _chunkSize = package.TotalFileSize / _chunkCount;
    }

    /// <summary>
    /// Creates a new chunk with the specified ID, start position, and end position.
    /// </summary>
    /// <param name="id">The unique identifier for the chunk.</param>
    /// <param name="start">The start position of the chunk.</param>
    /// <param name="end">The end position of the chunk.</param>
    /// <returns>A new instance of the <see cref="Chunk"/> class.</returns>
    private Chunk GetChunk(string id, long start, long end)
    {
        Chunk chunk = new(start, end) {
            Id = id,
            MaxTryAgainOnFailure = _config.MaxTryAgainOnFailure,
            Timeout = _config.Timeout
        };

        return chunk;
    }
}