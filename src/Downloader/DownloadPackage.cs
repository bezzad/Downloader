using Downloader.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

public class PackageInfo
{
    /// <summary>
    /// Gets or sets the total size of the file to be downloaded.
    /// </summary>
    public long TotalFileSize { get; set; }

    /// <summary>
    /// Gets or sets the chunks of the file being downloaded.
    /// </summary>
    public Chunk[] Chunks { get; set; }
}

/// <summary>
/// Represents a package containing information about a download operation.
/// </summary>
public class DownloadPackage : PackageInfo, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);

    /// <summary>
    /// Gets or sets a value indicating whether the package is currently being saved.
    /// </summary>
    [JsonIgnore] public bool IsSaving => Status is DownloadStatus.Running or DownloadStatus.Paused;

    /// <summary>
    /// Gets or sets a value indicating whether the save operation is complete.
    /// </summary>
    [JsonIgnore] public bool IsSaveComplete => Status is DownloadStatus.Completed;

    /// <summary>
    /// Gets or sets the progress of the save operation.
    /// </summary>
    [JsonIgnore] public double SaveProgress { get; set; }

    /// <summary>
    /// Gets or sets the status of the download operation.
    /// </summary>
    public DownloadStatus Status { get; set; } = DownloadStatus.None;

    /// <summary>
    /// Gets or sets the URLs from which the file is being downloaded.
    /// </summary>
    public string[] Urls { get; set; }

    /// <summary>
    /// Gets or sets the name of the file to be saved.
    /// </summary>
    public string FileName { get; set; }

    public string DownloadingFileExtension { get => field ?? ""; set; }
    [JsonIgnore] public string DownloadingFileName => FileName + DownloadingFileExtension;

    /// <summary>
    /// Gets the total size of the received bytes.
    /// </summary>
    [JsonIgnore] public long ReceivedBytesSize => Chunks?.Sum(chunk => chunk.Position) ?? 0;

    /// <summary>
    /// Gets or sets a value indicating whether the download supports range requests.
    /// </summary>
    public bool IsSupportDownloadInRange { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether the download is being stored in memory.
    /// </summary>
    [JsonIgnore] public bool IsMemoryStream => string.IsNullOrWhiteSpace(FileName);

    [JsonIgnore] public bool IsFileStream => !IsMemoryStream;

    /// <summary>
    /// Gets or sets the storage for the download.
    /// </summary>
    public ConcurrentStream Storage { get; set; }

    /// <summary>
    /// Clears the chunks and resets the package.
    /// </summary>
    public void ClearChunks()
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
        // Read the field ONCE: a terminal transition or a dispose running on another thread sets
        // Storage to null, and a check-then-use on the property throws a NullReferenceException at
        // exactly the moment a download is being torn down.
        ConcurrentStream storage = Storage;
        if (storage is not null)
            await FlushInternalAsync(storage).ConfigureAwait(false);
    }

    /// <summary>
    /// Flush and close the storage asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous flush operation.</returns>
    public async Task CloseAsync()
    {
        ConcurrentStream storage = Storage;
        if (storage is not null)
        {
            Storage = null; // no other closer can reach this stream once it is claimed
            await FlushInternalAsync(storage).ConfigureAwait(false);
            await storage.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task FlushInternalAsync(ConcurrentStream storage)
    {
        if (storage.CanWrite)
        {
            await storage.FlushAsync().ConfigureAwait(false);
            await Task.Delay(20).ConfigureAwait(false); // Add a small delay to ensure file is fully written
        }
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
    /// <param name="maxMemoryBufferBytes">The maximum size of the memory buffer in bytes.</param>
    /// <param name="cancellation">The cancellation token to use for cancelling operations.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public void BuildStorage(long maxMemoryBufferBytes, ILogger logger = null)
    {
        Storage = string.IsNullOrWhiteSpace(DownloadingFileName)
            ? new ConcurrentStream(maxMemoryBufferBytes, logger)
            : new ConcurrentStream(DownloadingFileName, TotalFileSize, maxMemoryBufferBytes, logger);
    }

    public void SetState(DownloadStatus state)
    {
        try
        {
            _stateSemaphore.Wait();
            Status = state;
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    public async Task<bool> TrySetCompleteState(DownloadStatus state, bool clearPackageOnCompletionWithFailure = false)
    {
        try
        {
            await _stateSemaphore.WaitAsync().ConfigureAwait(false);

            if (Status is DownloadStatus.Failed or DownloadStatus.Completed) // check old state
                return false; // Can't change this status

            Status = state;

            if (IsFileStream)
                await CloseAsync().ConfigureAwait(false);
            else
                await FlushAsync().ConfigureAwait(false);

            if (Status is DownloadStatus.Failed &&
                clearPackageOnCompletionWithFailure)
            {
                await DisposeAsync().ConfigureAwait(false);
                if (IsFileStream)
                    FileHelper.DeleteFile(DownloadingFileName);
            }
            else if (Status is DownloadStatus.Completed)
            {
                if (IsFileStream && !DownloadingFileName.Equals(FileName))
                {
                    if (File.Exists(FileName))
                        FileHelper.DeleteFile(FileName);

                    File.Move(DownloadingFileName, FileName);
                }
                ClearChunks();
            }

            return true;
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    /// <summary>
    /// Disposes of the download package, clearing the chunks and disposing of the storage.
    /// </summary>
    public void Dispose()
    {
        ClearChunks();
        Storage?.Dispose();
    }

    /// <summary>
    /// Disposes of the download package, clearing the chunks and disposing of the storage.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        ClearChunks();
        await CloseAsync().ConfigureAwait(false);
    }
}