using Downloader.Extensions;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.ComponentModel;

namespace Downloader;

/// <summary>
/// Concrete implementation of the <see cref="AbstractDownloadService"/> class.
/// </summary>
public class DownloadService : AbstractDownloadService
{
    // The first genuine (non-cancellation) chunk error of the current attempt. Captured by
    // DownloadChunk so StartDownload can decide the terminal state — and whether to attempt a
    // single-connection fallback — instead of the chunk committing "Failed" immediately (issue #231).
    private Exception _chunkError;

    // Guards against looping: the single-connection fallback is attempted at most once per download.
    private bool _triedSingleConnectionFallback;

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

            _chunkError = null;
            _triedSingleConnectionFallback = false;

            Request firstRequest = RequestInstances.First();
            Logger?.LogDebug("Resolving remote file info (size + range support) from first request");
            // Single canonical probe for size + range support (and the file name, cached on the
            // request) instead of resolving each concept separately. Shared with RemoteFileResolver.
            RemoteFileInfo fileInfo = await Client.GetFileInfoAsync(firstRequest, GlobalCancellationTokenSource.Token).ConfigureAwait(false);
            Package.TotalFileSize = fileInfo.FileSize;
            Package.IsSupportDownloadInRange = fileInfo.SupportsRange;
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
                                      Package.Storage.IsCanceled ||
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

            // First attempt with the configured parallel/serial + chunk settings.
            await RunChunksAsync().ConfigureAwait(false);

            // issue #231: parallel/multi-connection HTTPS downloads can fail in environments where a
            // TLS-inspecting proxy/antivirus breaks concurrent connections (SEC_E_DECRYPT_FAILURE,
            // "response ended prematurely", aborted sockets) even though a single sequential
            // connection works. When the multi-connection attempt fails with a transient transport
            // error, retry once over a single connection before giving up.
            if (ShouldFallbackToSingleConnection())
            {
                Logger?.LogWarning(_chunkError,
                    "Multi-connection download failed with a transient transport error; " +
                    "retrying over a single connection.");
                PrepareSingleConnectionFallback();
                await RunChunksAsync().ConfigureAwait(false);
            }

            if (_chunkError is null && IsCancelled)
            {
                Logger?.LogWarning(null, "Download was cancelled");
                await SendDownloadCompletionSignal(DownloadStatus.Stopped).ConfigureAwait(false);
            }
            else if (_chunkError is null && Status is DownloadStatus.Running)
            {
                Logger?.LogInformation("Download completed successfully");
                // For unknown-size downloads (server omitted Content-Length, TotalFileSize stayed 0
                // during the whole transfer), the real size is only known once every byte has been
                // received. Finalize it here so storage truncation below and the final progress
                // reflect the actual length instead of leaving it at 0 (issue #230).
                if (Package.TotalFileSize <= 0)
                    Package.TotalFileSize = Package.ReceivedBytesSize;

                if (Package.IsFileStream)
                {
                    // Remove Package bytes from end of file stream
                    await Package.FlushAsync();
                    Package.Storage?.SetLength(Package.TotalFileSize);
                }

                await SendDownloadCompletionSignal(DownloadStatus.Completed).ConfigureAwait(false);
            }
            else if (_chunkError is not null)
            {
                // A chunk failed (and the single-connection fallback, if any, did too).
                Logger?.LogError(_chunkError, "Download failed with error: {ErrorMessage}", _chunkError.Message);
                await SendDownloadCompletionSignal(DownloadStatus.Failed, _chunkError).ConfigureAwait(false);
            }
            else
            {
                // Unknown/unexpected terminal state — log a warning instead of breaking into the
                // debugger (Debugger.Break() must never ship in a library: it would halt the
                // consumer's app under a debugger).
                Logger?.LogWarning("Download finished in an unexpected state: {Status}", Status);
            }
        }
        catch (Exception exp)
        {
            // issue #225: A network/SSL exception thrown while the token was being cancelled
            // should be surfaced as Stopped, not Failed.
            if (IsCancelled || exp is OperationCanceledException or TaskCanceledException)
            {
                Logger?.LogWarning(exp, "Download was cancelled");
                await SendDownloadCompletionSignal(DownloadStatus.Stopped, exp).ConfigureAwait(false);
            }
            else
            {
                Logger?.LogError(exp, "Download failed with error: {ErrorMessage}", exp.Message);
                await SendDownloadCompletionSignal(DownloadStatus.Failed, exp).ConfigureAwait(false);
            }
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
                        FileHelper.DeleteFile(Package.DownloadingFileName);
                }
                else
                {
                    FileHelper.DeleteFile(Package.DownloadingFileName);
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
            long metadataSize = streamLength - Package.TotalFileSize;

            if (metadataSize < 1 || metadataSize > int.MaxValue)
                return false;

            // Rent a buffer from the pool — avoids GC allocation
            byte[] rented = ArrayPool<byte>.Shared.Rent((int)metadataSize);

            try
            {
                Memory<byte> buffer = rented.AsMemory(0, (int)metadataSize);

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
                var packageInfo = Serializer.Deserialize(rented, 0, (int)metadataSize);
                if (packageInfo?.TotalFileSize != Package.TotalFileSize) // file on server was changed!
                    return false;

                Package.Chunks = packageInfo.Chunks;
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
    /// Runs the chunk downloads (parallel or serial per <see cref="AbstractDownloadService.Options"/>).
    /// A genuine chunk failure is captured in <see cref="_chunkError"/> by <see cref="DownloadChunk"/>
    /// and swallowed here so the caller can decide the terminal state (and whether to fall back to a
    /// single connection). User cancellations and non-chunk failures (where <see cref="_chunkError"/>
    /// stays null) are not swallowed and propagate to the caller's exception handler.
    /// </summary>
    private async Task RunChunksAsync()
    {
        try
        {
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
        }
        catch (Exception) when (_chunkError is not null)
        {
            // A chunk captured the originating error and cancelled its siblings; that cancellation
            // surfaces here as the same error or an OperationCanceledException. Swallow it — the
            // terminal state is decided from _chunkError by the caller.
        }
    }

    /// <summary>
    /// Determines whether the failed multi-connection attempt should be retried over a single
    /// sequential connection. Only transient transport failures qualify, and only once (issue #231).
    /// </summary>
    private bool ShouldFallbackToSingleConnection()
    {
        // Never trade already-downloaded, resumable bytes for a blind single-connection restart:
        // the fallback rebuilds a fresh whole-file chunk (position 0), so with progress in the
        // package it would wipe the resume state — a transient timeout mid-download or on a resume
        // attempt must leave the package resumable from its last position, not restart at 0%.
        return _chunkError is not null
               && !_triedSingleConnectionFallback
               && Package.ReceivedBytesSize == 0
               && (Options.ParallelDownload || Options.ChunkCount > 1)
               && _chunkError.IsMomentumError();
    }

    /// <summary>
    /// Reconfigures the download to use a single sequential connection and resets the per-attempt
    /// state so a fresh download can run (issue #231).
    /// </summary>
    private void PrepareSingleConnectionFallback()
    {
        _triedSingleConnectionFallback = true;
        _chunkError = null;

        SetSingleChunkDownload(); // ChunkCount=1, ParallelCount=1, ParallelDownload=false, fresh ParallelSemaphore
        Package.ClearChunks(); // discard the partial parallel chunks; rebuilt below as a single chunk
        RenewGlobalCancellationTokenSource(); // the failed attempt canceled the previous token
        Package.SetState(DownloadStatus.Running);
        ChunkHub.SetFileChunks(Package); // rebuild as a single chunk over the whole file/range
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

    /// <summary>
    /// Downloads the file in serial chunks.
    /// </summary>
    /// <param name="pauseToken">The pause token for pausing the download.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task SerialDownload(PauseToken pauseToken)
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
            // Capture the first genuine chunk error and stop the sibling chunks. The terminal state
            // (and any single-connection fallback) is decided in StartDownload — not here — so the
            // download is not prematurely marked Failed before a fallback can run (issue #231).
            Interlocked.CompareExchange(ref _chunkError, exp, null);
            cancellationTokenSource.Cancel(false); // stop sibling chunks
            throw;
        }
        finally
        {
            ParallelSemaphore.Release();
        }
    }
}