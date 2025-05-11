using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents a package containing information about a download operation.
/// </summary>
public class DownloadPackage : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets or sets a value indicating whether the package is currently being saved.
    /// </summary>
    public bool IsSaving { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the save operation is complete.
    /// </summary>
    public bool IsSaveComplete { get; set; }

    /// <summary>
    /// Gets or sets the progress of the save operation.
    /// </summary>
    public double SaveProgress { get; set; }

    /// <summary>
    /// Gets or sets the status of the download operation.
    /// </summary>
    public DownloadStatus Status { get; set; } = DownloadStatus.None;

    /// <summary>
    /// Gets or sets the URLs from which the file is being downloaded.
    /// </summary>
    public string[] Urls { get; set; }

    /// <summary>
    /// Gets or sets the total size of the file to be downloaded.
    /// </summary>
    public long TotalFileSize { get; set; }

    /// <summary>
    /// Gets or sets the name of the file to be saved.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the chunks of the file being downloaded.
    /// </summary>
    public Chunk[] Chunks { get; set; }

    /// <summary>
    /// Gets the total size of the received bytes.
    /// </summary>
    public long ReceivedBytesSize => Chunks?.Sum(chunk => chunk.Position) ?? 0;

    /// <summary>
    /// Gets or sets a value indicating whether the download supports range requests.
    /// </summary>
    public bool IsSupportDownloadInRange { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether the download is being stored in memory.
    /// </summary>
    public bool InMemoryStream => string.IsNullOrWhiteSpace(FileName);

    /// <summary>
    /// Gets or sets the storage for the download.
    /// </summary>
    public ConcurrentStream Storage { get; set; }

    /// <summary>
    /// Clears the chunks and resets the package.
    /// </summary>
    public void Clear()
    {
        if (Chunks != null)
        {
            foreach (Chunk chunk in Chunks)
                chunk.Clear();
        }
        Chunks = null;
    }

    /// <summary>
    /// Flushes the storage asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous flush operation.</returns>
    public async Task FlushAsync()
    {
        if (Storage?.CanWrite == true)
            await Storage.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Validates the chunks and ensures they are in the correct position.
    /// </summary>
    public void Validate()
    {
        foreach (Chunk chunk in Chunks)
        {
            if (chunk.IsValidPosition() == false)
            {
                long realLength = Storage?.Length ?? 0;
                if (realLength <= chunk.Position)
                {
                    chunk.Clear();
                }
            }

            if (!IsSupportDownloadInRange)
                chunk.Clear();
        }
    }

    /// <summary>
    /// Builds the storage for the download package.
    /// </summary>
    /// <param name="reserveFileSize">Indicates whether to reserve the file size.</param>
    /// <param name="maxMemoryBufferBytes">The maximum size of the memory buffer in bytes.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public void BuildStorage(bool reserveFileSize, long maxMemoryBufferBytes = 0, ILogger logger = null)
    {
        Storage = string.IsNullOrWhiteSpace(FileName)
            ? new ConcurrentStream(maxMemoryBufferBytes, logger)
            : new ConcurrentStream(FileName, reserveFileSize ? TotalFileSize : 0, maxMemoryBufferBytes, logger);
    }

    /// <summary>
    /// Disposes of the download package, clearing the chunks and disposing of the storage.
    /// </summary>
    public void Dispose()
    {
        Clear();
        Storage?.Dispose();
    }

    /// <summary>
    /// Disposes of the download package, clearing the chunks and disposing of the storage.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Clear();
        if (Storage is not null)
        {
            await Storage.DisposeAsync().ConfigureAwait(false);
        }
    }
}