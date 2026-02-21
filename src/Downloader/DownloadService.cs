using Downloader.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    protected override async Task<Stream> StartDownload(bool forceBuildStorage = true, string filename = null)
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

            if (!await ProvideDownloadOnFile(filename).ConfigureAwait(false))
            {
                Logger?.LogWarning(null, "Download was ignored because of FileExistPolicy");
                await SendDownloadCompletionSignal(DownloadStatus.Stopped).ConfigureAwait(false);
                return null;
            }

            // Check if we need to rebuild storage
            bool needToBuildStorage = forceBuildStorage ||
                                      Package.Storage is null ||
                                      Package.Storage.IsDisposed ||
                                      (Package.Storage.Length == 0 && Package.Chunks?.Any(c => c.Position > 0) == true);

            if (needToBuildStorage)
            {
                Logger?.LogDebug("Building storage with reserving storage space, MaxMemoryBuffer={MaxMemoryBuffer}",
                    Options.MaximumMemoryBufferBytes);
                Package.BuildStorage(Options.MaximumMemoryBufferBytes, Logger);
            }

            ValidateBeforeChunking();
            ChunkHub.SetFileChunks(Package);

            Logger?.LogInformation("Starting download the file with size {TotalFileSize}B on {Path}",
                Package.TotalFileSize, Package.IsMemoryStream ? "MemoryStream" : Package.DownloadingFileName);
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

            if (GlobalCancellationTokenSource.IsCancellationRequested)
            {
                Logger?.LogWarning(null, "Download was cancelled");
                await SendDownloadCompletionSignal(DownloadStatus.Stopped).ConfigureAwait(false);
            }
            else if (Status is DownloadStatus.Running)
            {
                Logger?.LogInformation("Download completed successfully");
                if (Package.IsFileStream)
                {
                    // Remove Package bytes from end of file stream
                    await Package.FlushAsync();
                    Package.Storage?.SetLength(Package.TotalFileSize);
                }

                await SendDownloadCompletionSignal(DownloadStatus.Completed).ConfigureAwait(false);
            }
            else
            {
                // Unknown STATE!
                Logger?.LogInformation("Download completed but isn't successful!");
                Debugger.Break();
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

    private async Task<bool> ProvideDownloadOnFile(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            Package.FileName = fileName;
            Package.DownloadingFileExtension = Options.DownloadFileExtension;
            string dirName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(dirName))
            {
                Directory.CreateDirectory(dirName); // ensure the folder is existing
            }

            if (!Package.CheckFileExistPolicy(Options.FileExistPolicy))
                return false;

            if (File.Exists(Package.DownloadingFileName))
            {
                if (Options.EnableAutoResumeDownload)
                {
                    bool canContinue = await TryResumeFromExistingFile().ConfigureAwait(false);
                    if (!canContinue)
                        File.Delete(Package.DownloadingFileName);
                }
                else
                {
                    File.Delete(Package.DownloadingFileName);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Attempts to resume download from an existing .download file using saved metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task<bool> TryResumeFromExistingFile()
    {
        try
        {
            if (Package.TotalFileSize < 1)
                return false;

            await using FileStream stream = new(Package.DownloadingFileName, FileMode.Open, FileAccess.Read);
            long streamLength = stream.Length;
            int metadataSize = (int)(streamLength - Package.TotalFileSize);

            if (metadataSize <= 0)
                return false;

            // Rent a buffer from the pool — avoids GC allocation
            byte[] rented = ArrayPool<byte>.Shared.Rent(metadataSize);

            try
            {
                Memory<byte> buffer = rented.AsMemory(0, metadataSize);

                stream.Seek(Package.TotalFileSize, SeekOrigin.Begin);

                // Read exactly metadataSize bytes (ReadAsync may return fewer in one call)
                int totalRead = 0;
                while (totalRead < metadataSize)
                {
                    int read = await stream
                        .ReadAsync(buffer.Slice(totalRead), GlobalCancellationTokenSource.Token)
                        .ConfigureAwait(false);

                    if (read == 0)
                        break; // unexpected end of stream

                    totalRead += read;
                }

                if (totalRead < metadataSize)
                    return false; // incomplete metadata

                // Deserialize only the exact slice (not the full rented array)
                var package = Serializer.Deserialize<DownloadPackage>(rented.AsSpan(0, metadataSize).ToArray());
                if (package?.TotalFileSize != Package.TotalFileSize) // file on server was changed!
                    return false;

                Package.Chunks = package.Chunks;
                return true;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
        catch (Exception ex)
        {
            Logger?.LogWarning(ex, "Failed to read resume metadata from existing .download file, starting fresh download");
            return false;
        }
    }

    /// <summary>
    /// Sends the download completion signal with the specified <paramref name="state"/> and optional <paramref name="error"/>.
    /// </summary>
    /// <param name="state">The state of the download operation.</param>
    /// <param name="error">The exception that caused the download to fail, if any.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SendDownloadCompletionSignal(DownloadStatus state, Exception error = null)
    {
        if (await Package.TrySetCompleteState(state, Options.ClearPackageOnCompletionWithFailure).ConfigureAwait(false))
        {
            OnDownloadFileCompleted(new AsyncCompletedEventArgs(error, state is DownloadStatus.Stopped, Package));
        }
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
        if (Options.CheckDiskSizeBeforeDownload && Package.IsFileStream)
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
        Options.ParallelDownload = false;
        ParallelSemaphore = new SemaphoreSlim(1, 1);
    }

    /// <summary>
    /// Downloads the file in parallel chunks with controlled concurrency.
    /// </summary>
    /// <param name="pauseToken">The pause token for pausing the download.</param>
    private async Task ParallelDownload(PauseToken pauseToken)
    {
        try
        {
            List<Task> chunkTasks = GetChunksTasks(pauseToken).ToList();
            int maxConcurrentTasks = Math.Min(Options.ParallelCount, chunkTasks.Count);
            Logger?.LogDebug("Starting parallel download with {MaxConcurrentTasks} concurrent tasks",
                maxConcurrentTasks);

            ParallelOptions options = new() {
                MaxDegreeOfParallelism = maxConcurrentTasks,
                CancellationToken = GlobalCancellationTokenSource.Token
            };

            await Parallel.ForEachAsync(chunkTasks, options, async (task, _) => {
                await task.ConfigureAwait(false);
            }).ConfigureAwait(false);
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
                // Check for cancellation after each chunk
                if (GlobalCancellationTokenSource.Token.IsCancellationRequested)
                {
                    Logger?.LogInformation("Download cancelled during serial processing");
                    return;
                }

                await task.ConfigureAwait(false);
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
            if (cancellationTokenSource.IsCancellationRequested)
                return chunk;

            return await chunkDownloader.Download(request, pause, cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (Exception exp) when (!cancellationTokenSource.IsCancellationRequested)
        {
            Logger?.LogError(exp, "Error during download: {ErrorMessage}", exp.Message);
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