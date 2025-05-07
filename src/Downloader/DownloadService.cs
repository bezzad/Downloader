using Downloader.Extensions.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Concrete implementation of the <see cref="AbstractDownloadService"/> class.
/// </summary>
public class DownloadService : AbstractDownloadService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadService"/> class with the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the download service.</param>
    /// <param name="loggerFactory">Pass standard logger factory</param>
    public DownloadService(DownloadConfiguration options, ILoggerFactory loggerFactory = null) : base(options)
    {
        Logger = loggerFactory?.CreateLogger<DownloadService>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadService"/> class with default options.
    /// </summary>
    public DownloadService(ILoggerFactory loggerFactory = null) : this(null, loggerFactory) { }

    /// <summary>
    /// Starts the download operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    protected override async Task<Stream> StartDownload(bool forceBuildStorage = true)
    {
        try
        {
            Logger?.LogInformation("Starting download process with forceBuildStorage={ForceBuildStorage}",
                forceBuildStorage);
            await SingleInstanceSemaphore.WaitAsync().ConfigureAwait(false);

            Request firstRequest = RequestInstances.First();
            Logger?.LogDebug("Getting file size from first request");
            Package.TotalFileSize = await Client.GetFileSizeAsync(firstRequest).ConfigureAwait(false);
            Package.IsSupportDownloadInRange =
                await Client.IsSupportDownloadInRange(firstRequest).ConfigureAwait(false);
            Logger?.LogInformation("File size: {TotalFileSize}, Supports range download: {IsSupportDownloadInRange}",
                Package.TotalFileSize, Package.IsSupportDownloadInRange);

            // Check if we need to rebuild storage
            bool needToBuildStorage = forceBuildStorage ||
                                      Package.Storage is null ||
                                      Package.Storage.IsDisposed ||
                                      (Package.Storage.Length == 0 && Package.Chunks?.Any(c => c.Position > 0) == true);

            if (needToBuildStorage)
            {
                Logger?.LogDebug("Building storage with ReserveStorageSpace={ReserveStorage}, MaxMemoryBuffer={MaxMemoryBuffer}",
                    Options.ReserveStorageSpaceBeforeStartingDownload, Options.MaximumMemoryBufferBytes);
                Package.BuildStorage(Options.ReserveStorageSpaceBeforeStartingDownload,
                    Options.MaximumMemoryBufferBytes);
            }

            ValidateBeforeChunking();
            ChunkHub.SetFileChunks(Package);

            Logger?.LogInformation("Starting download of {FileName} with size {TotalFileSize}",
                Package.FileName, Package.TotalFileSize);
            OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

            if (Options.ParallelDownload)
            {
                Logger?.LogDebug("Starting parallel download with {ChunkCount} chunks", Package.Chunks?.Length);
                await ParallelDownload(PauseTokenSource.Token).ConfigureAwait(false);
            }
            else
            {
                Logger?.LogDebug("Starting serial download with {ChunkCount} chunks", Package.Chunks?.Length);
                await SerialDownload(PauseTokenSource.Token).ConfigureAwait(false);
            }

            if (Status == DownloadStatus.Running)
            {
                Logger?.LogInformation("Download completed successfully");
                await SendDownloadCompletionSignal(DownloadStatus.Completed).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException exp)
        {
            Logger?.LogWarning(exp, "Download was cancelled");
            await SendDownloadCompletionSignal(DownloadStatus.Stopped, exp).ConfigureAwait(false);
        }
        catch (Exception exp)
        {
            Logger?.LogError(exp, "Download failed with error: {ErrorMessage}", exp.Message);
            await SendDownloadCompletionSignal(DownloadStatus.Failed, exp).ConfigureAwait(false);
        }
        finally
        {
            SingleInstanceSemaphore.Release();
            Logger?.LogDebug("Download process completed, semaphore released");
        }

        return Package.Storage?.OpenRead();
    }

    /// <summary>
    /// Sends the download completion signal with the specified <paramref name="state"/> and optional <paramref name="error"/>.
    /// </summary>
    /// <param name="state">The state of the download operation.</param>
    /// <param name="error">The exception that caused the download to fail, if any.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendDownloadCompletionSignal(DownloadStatus state, Exception error = null)
    {
        if (Status == DownloadStatus.Failed)
        {
            return; // another event throws before this
        }

        Status = state; 
        bool isCancelled = Status == DownloadStatus.Stopped;
        Package.IsSaveComplete = Status == DownloadStatus.Completed && error == null;
        Package.IsSaving = false; // Reset IsSaving flag regardless of status
        await (Package?.Storage?.FlushAsync() ?? Task.FromResult(0)).ConfigureAwait(false);
        if (Package?.Storage != null)
        {
            await Task.Delay(100); // Add a small delay to ensure file is fully written
        }

        OnDownloadFileCompleted(new AsyncCompletedEventArgs(error, isCancelled, Package));
    }

    /// <summary>
    /// Validates the download configuration before chunking the file.
    /// </summary>
    private void ValidateBeforeChunking()
    {
        CheckSingleChunkDownload();
        CheckSupportDownloadInRange();
        SetRangedSizes();
        CheckSizes();
    }

    /// <summary>
    /// Sets the range sizes for the download operation.
    /// </summary>
    private void SetRangedSizes()
    {
        if (Options.RangeDownload)
        {
            if (!Package.IsSupportDownloadInRange)
            {
                throw new NotSupportedException(
                    "The server of your desired address does not support download in a specific range");
            }

            if (Options.RangeHigh < Options.RangeLow)
            {
                Options.RangeLow = Options.RangeHigh - 1;
            }

            if (Options.RangeLow < 0)
            {
                Options.RangeLow = 0;
            }

            if (Options.RangeHigh < 0)
            {
                Options.RangeHigh = Options.RangeLow;
            }

            if (Package.TotalFileSize > 0)
            {
                Options.RangeHigh = Math.Min(Package.TotalFileSize, Options.RangeHigh);
            }

            Package.TotalFileSize = Options.RangeHigh - Options.RangeLow + 1;
        }
        else
        {
            Options.RangeHigh = Options.RangeLow = 0; // reset range options
        }
    }

    /// <summary>
    /// Checks if there is enough disk space before starting the download.
    /// </summary>
    private void CheckSizes()
    {
        if (Options.CheckDiskSizeBeforeDownload && !Package.InMemoryStream)
        {
            FileHelper.ThrowIfNotEnoughSpace(Package.TotalFileSize, Package.FileName);
        }
    }

    /// <summary>
    /// Checks if the download should be handled as a single chunk.
    /// </summary>
    private void CheckSingleChunkDownload()
    {
        if (Package.TotalFileSize <= 1)
            Package.TotalFileSize = 0;

        if (Package.TotalFileSize <= Options.MinimumSizeOfChunking)
            SetSingleChunkDownload();
    }

    /// <summary>
    /// Checks if the server supports download in a specific range.
    /// </summary>
    private void CheckSupportDownloadInRange()
    {
        if (Package.IsSupportDownloadInRange == false)
            SetSingleChunkDownload();
    }

    /// <summary>
    /// Sets the download configuration to handle the file as a single chunk.
    /// </summary>
    private void SetSingleChunkDownload()
    {
        Options.ChunkCount = 1;
        Options.ParallelCount = 1;
        ParallelSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Downloads the file in parallel chunks.
    /// </summary>
    /// <param name="pauseToken">The pause token for pausing the download.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ParallelDownload(PauseToken pauseToken)
    {
        try
        {
            List<Task> chunkTasks = GetChunksTasks(pauseToken).ToList();
            int maxConcurrentTasks = Math.Min(Options.ParallelCount, chunkTasks.Count);

            Logger?.LogDebug("Starting parallel download with {MaxConcurrentTasks} concurrent tasks",
                maxConcurrentTasks);

            // Process tasks in batches to prevent overwhelming the system
            for (int i = 0; i < chunkTasks.Count; i += maxConcurrentTasks)
            {
                IEnumerable<Task> batch = chunkTasks.Skip(i).Take(maxConcurrentTasks);
                await Task.WhenAll(batch).ConfigureAwait(false);

                // Check for cancellation after each batch
                if (GlobalCancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger?.LogInformation("Download cancelled during batch processing");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error during parallel download: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Downloads the file in serial chunks.
    /// </summary>
    /// <param name="pauseToken">The pause token for pausing the download.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SerialDownload(PauseToken pauseToken)
    {
        try
        {
            List<Task> chunkTasks = GetChunksTasks(pauseToken).ToList();
            Logger?.LogDebug("Starting serial download with {ChunkCount} chunks", chunkTasks.Count);

            foreach (Task task in chunkTasks)
            {
                await task.ConfigureAwait(false);

                // Check for cancellation after each chunk
                if (GlobalCancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger?.LogInformation("Download cancelled during serial processing");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error during serial download: {ErrorMessage}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Gets the tasks for downloading the chunks.
    /// </summary>
    /// <param name="pauseToken">The pause token for pausing the download.</param>
    /// <returns>An enumerable collection of tasks representing the chunk downloads.</returns>
    private IEnumerable<Task> GetChunksTasks(PauseToken pauseToken)
    {
        for (int i = 0; i < Package.Chunks.Length; i++)
        {
            Request request = RequestInstances[i % RequestInstances.Count];
            yield return DownloadChunk(Package.Chunks[i], request, pauseToken, GlobalCancellationTokenSource);
        }
    }

    /// <summary>
    /// Downloads a specific chunk of the file.
    /// </summary>
    /// <param name="chunk">The chunk to download.</param>
    /// <param name="request">The request to use for the download.</param>
    /// <param name="pause">The pause token for pausing the download.</param>
    /// <param name="cancellationTokenSource">The cancellation token source for cancelling the download.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the downloaded chunk.</returns>
    private async Task<Chunk> DownloadChunk(Chunk chunk, Request request, PauseToken pause,
        CancellationTokenSource cancellationTokenSource)
    {
        ChunkDownloader chunkDownloader = new(chunk, Options, Package.Storage, Client, Logger);
        chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
        await ParallelSemaphore.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
        try
        {
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
            return await chunkDownloader.Download(request, pause, cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await SendDownloadCompletionSignal(DownloadStatus.Stopped).ConfigureAwait(false);
            throw;
        }
        catch (Exception exp)
        {
            await SendDownloadCompletionSignal(DownloadStatus.Failed, exp).ConfigureAwait(false);
            cancellationTokenSource.Cancel(false);
            throw;
        }
        finally
        {
            ParallelSemaphore.Release();
        }
    }
}