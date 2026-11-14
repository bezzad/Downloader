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

    /// <summary>
    /// Gets or sets the <c>ETag</c> the server sent for the file, or null. Used to see on resume
    /// that the file on the server was not changed.
    /// </summary>
    public string ETag { get; set; }

    /// <summary>
    /// Gets or sets the <c>Last-Modified</c> value the server sent for the file, or null.
    /// </summary>
    public string LastModified { get; set; }
}

/// <summary>
/// Represents a package containing information about a download operation.
/// </summary>
public class DownloadPackage : PackageInfo, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private readonly object _chunksSync = new();

    /// <summary>
    /// Gets a value indicating whether the package is currently being saved (status is Running or Paused).
    /// </summary>
    [JsonIgnore] public bool IsSaving => Status is DownloadStatus.Running or DownloadStatus.Paused;

    /// <summary>
    /// Gets a value indicating whether the save operation is complete (status is Completed).
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
    [JsonIgnore]
    public long ReceivedBytesSize
    {
        get
        {
            lock (_chunksSync)
                return Chunks?.Sum(chunk => chunk.Position) ?? 0;
        }
    }

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
        lock (_chunksSync)
        {
            if (Chunks != null)
            {
                foreach (Chunk chunk in Chunks)
                    chunk.Clear();
            }
            Chunks = null;
        }
    }

    /// <summary>
    /// Atomically splits the unconsumed tail of <paramref name="source"/> into two useful ranges.
    /// Already-consumed bytes remain owned by <paramref name="source"/>.
    /// </summary>
    internal bool TrySplitChunk(Chunk source, long minimumChunkSize, out Chunk tail)
    {
        tail = null;
        if (source is null || minimumChunkSize <= 0)
            return false;

        lock (_chunksSync)
        {
            int sourceIndex = Chunks is null ? -1 : Array.IndexOf(Chunks, source);
            long remaining = source.EmptyLength;
            long firstHalfLength = remaining / 2;
            if (sourceIndex < 0 || firstHalfLength < minimumChunkSize)
                return false;

            long splitStart = source.Start + source.Position + firstHalfLength;
            long previousEnd = source.End;
            tail = new Chunk(splitStart, previousEnd) {
                MaxTryAgainOnFailure = source.MaxTryAgainOnFailure,
                Timeout = source.Timeout
            };

            source.End = splitStart - 1;

            Chunk[] expanded = new Chunk[Chunks.Length + 1];
            Array.Copy(Chunks, 0, expanded, 0, sourceIndex + 1);
            expanded[sourceIndex + 1] = tail;
            Array.Copy(
                Chunks,
                sourceIndex + 1,
                expanded,
                sourceIndex + 2,
                Chunks.Length - sourceIndex - 1);
            Chunks = expanded;
            return true;
        }
    }

    /// <summary>
    /// Captures a range-consistent copy for durable resume metadata.
    /// </summary>
    internal PackageInfo CreateResumeMetadataSnapshot()
    {
        lock (_chunksSync)
        {
            return new PackageInfo {
                TotalFileSize = TotalFileSize,
                Chunks = Chunks?.Select(chunk => chunk.Snapshot()).ToArray(),
                ETag = ETag,
                LastModified = LastModified
            };
        }
    }

    /// <summary>
    /// Writes every block the chunks gave before this call to the storage and flushes it to the disk.
    /// </summary>
    /// <remarks>
    /// To save this package for a later resume while the download runs, use this order: serialize the
    /// package first, then await this method, then save the serialized bytes. Then the saved chunk
    /// positions never count bytes that are not on the disk.
    /// </remarks>
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
            try
            {
                await FlushInternalAsync(storage).ConfigureAwait(false);
            }
            finally
            {
                await storage.DisposeAsync().ConfigureAwait(false); // close the file even when the flush fails
            }
        }
    }

    private static async Task FlushInternalAsync(ConcurrentStream storage)
    {
        if (storage.CanWrite)
        {
            await storage.FlushAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Validates the chunks and ensures they are in the correct position.
    /// </summary>
    public void Validate()
    {
        lock (_chunksSync)
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
    }

    /// <summary>
    /// Builds the storage for the download package.
    /// </summary>
    /// <param name="maxMemoryBufferBytes">The maximum size of the memory buffer in bytes.</param>
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

    /// <summary>
    /// Sets a final state once: Completed, Failed or Stopped. The storage is flushed and closed first,
    /// and for Completed the file gets its final name. If that work fails, the state is Failed.
    /// </summary>
    /// <returns>False when the package was already Completed or Failed.</returns>
    public async Task<bool> TrySetCompleteState(DownloadStatus state, bool clearPackageOnCompletionWithFailure = false)
    {
        (bool changed, _) = await TryFinishAsync(state, clearPackageOnCompletionWithFailure).ConfigureAwait(false);
        return changed;
    }

    /// <summary>
    /// Does the work of the end of a download (flush, close, rename) and only then sets the final state,
    /// so an error in that work is still reported: the state is then Failed (for Completed) and the
    /// error is returned. The storage is always closed.
    /// </summary>
    /// <returns>Whether the state was set, and the error of the work, if any.</returns>
    internal async Task<(bool Changed, Exception Error)> TryFinishAsync(DownloadStatus state,
        bool clearPackageOnCompletionWithFailure = false)
    {
        await _stateSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status is DownloadStatus.Failed or DownloadStatus.Completed) // check old state
                return (false, null); // Can't change this status

            Exception error = null;
            bool onlyRenameFailed = false;
            try
            {
                if (IsFileStream)
                    await CloseAsync().ConfigureAwait(false);
                else
                    await FlushAsync().ConfigureAwait(false);

                if (state is DownloadStatus.Completed)
                {
                    onlyRenameFailed = true; // every byte is on the disk from here
                    MoveToFinalName();
                    onlyRenameFailed = false;
                }
            }
            catch (Exception exp)
            {
                error = exp;
                if (state is DownloadStatus.Completed)
                    state = DownloadStatus.Failed; // the file is not right, so it is not completed
            }

            Status = state;

            // A finished temp file is kept when only the rename failed: the bytes are not lost.
            if (state is DownloadStatus.Failed && clearPackageOnCompletionWithFailure && !onlyRenameFailed)
            {
                await DisposeAsync().ConfigureAwait(false);
                if (IsFileStream)
                    FileHelper.DeleteFile(DownloadingFileName);
            }
            else if (state is DownloadStatus.Completed)
            {
                ClearChunks();
            }

            return (true, error);
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    private void MoveToFinalName()
    {
        if (IsFileStream && !DownloadingFileName.Equals(FileName))
        {
            if (File.Exists(FileName))
                FileHelper.DeleteFile(FileName);

            File.Move(DownloadingFileName, FileName);
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