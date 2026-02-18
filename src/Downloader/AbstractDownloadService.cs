using Downloader.Extensions;
using Downloader.Serializer;
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
/// Abstract base class for download services implementing <see cref="IDownloadService"/> and <see cref="IDisposable"/>.
/// </summary>
public abstract class AbstractDownloadService : IDownloadService, IDisposable, IAsyncDisposable
{
    private long _lastPackageUpdateTime;
    protected readonly IBinarySerializer Serializer = new BsonSerializer();
    
    /// <summary>
    /// Logger instance for logging messages.
    /// </summary>
    protected ILogger Logger;

    /// <summary>
    /// Semaphore to control parallel downloads.
    /// </summary>
    protected SemaphoreSlim ParallelSemaphore;

    /// <summary>
    /// Semaphore to ensure single instance operations.
    /// </summary>
    protected readonly SemaphoreSlim SingleInstanceSemaphore = new(1, 1);

    /// <summary>
    /// Global cancellation token source for managing download cancellation.
    /// </summary>
    protected CancellationTokenSource GlobalCancellationTokenSource;

    /// <summary>
    /// Task completion source for managing asynchronous operations.
    /// </summary>
    private TaskCompletionSource<AsyncCompletedEventArgs> _taskCompletion;

    /// <summary>
    /// Pause token source for managing download pausing.
    /// </summary>
    protected readonly PauseTokenSource PauseTokenSource;

    /// <summary>
    /// Chunk hub for managing download chunks.
    /// </summary>
    protected ChunkHub ChunkHub;

    /// <summary>
    /// List of request instances for download operations.
    /// </summary>
    protected List<Request> RequestInstances;

    /// <summary>
    /// Bandwidth tracker for download speed calculations.
    /// </summary>
    private readonly Bandwidth _bandwidth;

    /// <summary>
    /// Configuration options for the download service.
    /// </summary>
    protected DownloadConfiguration Options { get; set; }

    /// <summary>
    /// Indicates whether the download service is currently busy.
    /// </summary>
    public bool IsBusy => Status == DownloadStatus.Running;

    /// <summary>
    /// Indicates whether the download operation has been canceled.
    /// </summary>
    public bool IsCancelled => GlobalCancellationTokenSource?.IsCancellationRequested == true;

    /// <summary>
    /// Indicates whether the download operation is paused.
    /// </summary>
    public bool IsPaused => PauseTokenSource.IsPaused;

    /// <summary>
    /// The download package containing the necessary information for the download.
    /// </summary>
    public DownloadPackage Package { get; private set; }

    /// <summary>
    /// Get the current status of the download operation.
    /// </summary>
    public DownloadStatus Status => Package?.Status ?? DownloadStatus.None;

    /// <summary>
    /// The Socket client for the download service.
    /// </summary>
    protected SocketClient Client { get; private set; }

    /// <summary>
    /// Event triggered when the download file operation is completed.
    /// </summary>
    public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;

    /// <summary>
    /// Event triggered when the download progress changes.
    /// </summary>
    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    /// <summary>
    /// Event triggered when the progress of a chunk download changes.
    /// </summary>
    public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;

    /// <summary>
    /// Event triggered when the download operation starts.
    /// </summary>
    public event EventHandler<DownloadStartedEventArgs> DownloadStarted;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbstractDownloadService"/> class with the specified options.
    /// </summary>
    /// <param name="options">The configuration options for the download service.</param>
    protected AbstractDownloadService(DownloadConfiguration options)
    {
        PauseTokenSource = new PauseTokenSource();
        _bandwidth = new Bandwidth();
        Options = options ?? new DownloadConfiguration();
        Package = new DownloadPackage();
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="package"/> and optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="package">The download package containing the necessary information for the download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public Task<Stream> DownloadFileTaskAsync(DownloadPackage package, CancellationToken cancellationToken = default)
    {
        return DownloadFileTaskAsync(package, package.Urls, cancellationToken);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="package"/> and <paramref name="address"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="package">The download package containing the necessary information for the download.</param>
    /// <param name="address">The URL address of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string address,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileTaskAsync(package, [address], cancellationToken);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="package"/> and <paramref name="urls"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="package">The download package containing the necessary information for the download.</param>
    /// <param name="urls">The array of URL addresses of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public virtual async Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string[] urls,
        CancellationToken cancellationToken = default)
    {
        Package = package;
        await InitialDownloader(cancellationToken, urls).ConfigureAwait(false);
        return await StartDownload(false).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="address"/> and optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="address">The URL address of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public Task<Stream> DownloadFileTaskAsync(string address, CancellationToken cancellationToken = default)
    {
        return DownloadFileTaskAsync([address], cancellationToken);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="urls"/> and optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="urls">The array of URL addresses of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public virtual async Task<Stream> DownloadFileTaskAsync(string[] urls,
        CancellationToken cancellationToken = default)
    {
        await InitialDownloader(cancellationToken, urls).ConfigureAwait(false);
        return await StartDownload().ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="address"/> and saves it to the specified <paramref name="fileName"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="address">The URL address of the file to download.</param>
    /// <param name="fileName">The name of the file to save the download as.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public Task DownloadFileTaskAsync(string address, string fileName, CancellationToken cancellationToken = default)
    {
        return DownloadFileTaskAsync([address], fileName, cancellationToken);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="urls"/> and saves it to the specified <paramref name="fileName"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="urls">The array of URL addresses of the file to download.</param>
    /// <param name="fileName">The name of the file to save the download as.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public virtual async Task DownloadFileTaskAsync(string[] urls, string fileName,
        CancellationToken cancellationToken = default)
    {
        await InitialDownloader(cancellationToken, urls).ConfigureAwait(false);
        await StartDownloadOnFile(fileName).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="address"/> and saves it to the specified <paramref name="folder"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="address">The URL address of the file to download.</param>
    /// <param name="folder">The directory to save the downloaded file in.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public Task DownloadFileTaskAsync(string address, DirectoryInfo folder,
        CancellationToken cancellationToken = default)
    {
        return DownloadFileTaskAsync([address], folder, cancellationToken);
    }

    /// <summary>
    /// Downloads a file asynchronously using the specified <paramref name="urls"/> and saves it to the specified <paramref name="folder"/>, with an optional <paramref name="cancellationToken"/>.
    /// </summary>
    /// <param name="urls">The array of URL addresses of the file to download.</param>
    /// <param name="folder">The directory to save the downloaded file in.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation.</returns>
    public virtual async Task DownloadFileTaskAsync(string[] urls, DirectoryInfo folder,
        CancellationToken cancellationToken = default)
    {
        await InitialDownloader(cancellationToken, urls).ConfigureAwait(false);
        string name = await Client.SetRequestFileNameAsync(RequestInstances.First()).ConfigureAwait(false);
        string filename = Path.Combine(folder.FullName, name);
        await StartDownloadOnFile(filename).ConfigureAwait(false);
    }

    /// <summary>
    /// Cancels the current download operation.
    /// </summary>
    public virtual void CancelAsync()
    {
        GlobalCancellationTokenSource?.Cancel(true);
        Resume();
    }

    /// <summary>
    /// Cancels the current download operation asynchronously.
    /// </summary>
    /// <returns>A task that represents the asynchronous cancellation operation.</returns>
    public virtual async Task CancelTaskAsync()
    {
        CancelAsync();
        if (_taskCompletion != null)
            await _taskCompletion.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Resumes the paused download operation.
    /// </summary>
    public virtual void Resume()
    {
        Package.SetState(DownloadStatus.Running);
        PauseTokenSource.Resume();
    }

    /// <summary>
    /// Pauses the current download operation.
    /// </summary>
    public virtual void Pause()
    {
        PauseTokenSource.Pause();
        Package.SetState(DownloadStatus.Paused);
    }

    /// <summary>
    /// Clears the current download operation, including cancellation and disposal of resources.
    /// </summary>
    /// <returns>A task that represents the asynchronous clear operation.</returns>
    public virtual async Task Clear()
    {
        try
        {
            if (IsBusy || IsPaused)
                await CancelTaskAsync().ConfigureAwait(false);

            await SingleInstanceSemaphore.WaitAsync().ConfigureAwait(false);

            ParallelSemaphore?.Dispose();
            GlobalCancellationTokenSource?.Dispose();
            _bandwidth.Reset();
            RequestInstances = null;

            if (_taskCompletion != null)
            {
                if (!_taskCompletion.Task.IsCompleted)
                    _taskCompletion.TrySetCanceled();

                _taskCompletion = null;
            }
            // Note: don't clear package from `DownloadService.Dispose()`.
            // Because maybe it will be used at another time.
        }
        finally
        {
            SingleInstanceSemaphore?.Release();
        }
    }

    /// <summary>
    /// Initializes the downloader with the specified <paramref name="cancellationToken"/> and <paramref name="addresses"/>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <param name="addresses">The array of URL addresses of the file to download.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    private async Task InitialDownloader(CancellationToken cancellationToken, params string[] addresses)
    {
        await Clear().ConfigureAwait(false);
        Package.SetState(DownloadStatus.Created);
        GlobalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _taskCompletion = new TaskCompletionSource<AsyncCompletedEventArgs>();
        Client = new SocketClient(Options);
        RequestInstances = addresses.Select(url => new Request(url, Options.RequestConfiguration)).ToList();
        Package.Urls = RequestInstances.Select(req => req.Address.OriginalString).ToArray();
        ChunkHub = new ChunkHub(Options);
        ParallelSemaphore = new SemaphoreSlim(Options.ParallelCount, Options.ParallelCount);
    }

    private async Task StartDownloadOnFile(string fileName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            Package.FileName = fileName;
            Package.DownloadingFileExtension = Options.DownloadFileExtension;
            string dirName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(dirName))
            {
                Directory.CreateDirectory(dirName); // ensure the folder is existing
                await Task.Delay(100); // Add a small delay to ensure directory creation is complete
            }

            if (!Package.CheckFileExistPolicy(Options.FileExistPolicy))
                return;

            if (File.Exists(Package.DownloadingFileName))
            {
                if (Options.EnableResumeDownload)
                {
                    await TryResumeFromExistingFile().ConfigureAwait(false);
                }
                else
                {
                    File.Delete(Package.DownloadingFileName);
                }
            }
        }

        await StartDownload().ConfigureAwait(false);
    }

    /// <summary>
    /// Starts the download operation.
    /// </summary>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    protected abstract Task<Stream> StartDownload(bool forceBuildStorage = true);

    /// <summary>
    /// Attempts to resume download from an existing .download file using saved metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task TryResumeFromExistingFile();

    /// <summary>
    /// Raises the <see cref="DownloadStarted"/> event.
    /// </summary>
    /// <param name="e">The event arguments for the download started event.</param>
    protected void OnDownloadStarted(DownloadStartedEventArgs e)
    {
        Package.SetState(DownloadStatus.Running);
        DownloadStarted?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the <see cref="DownloadFileCompleted"/> event.
    /// </summary>
    /// <param name="e">The event arguments for the download file completed event.</param>
    protected void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
    {
        _taskCompletion.TrySetResult(e);
        DownloadFileCompleted?.Invoke(this, e);
    }

    /// <summary>
    /// Raises the <see cref="ChunkDownloadProgressChanged"/> and <see cref="DownloadProgressChanged"/> events in a unified way.
    /// </summary>
    /// <param name="e">The event arguments for the download progress changed event.</param>
    private void RaiseProgressChangedEvents(DownloadProgressChangedEventArgs e)
    {
        if (e.ReceivedBytesSize > Package.TotalFileSize)
            Package.TotalFileSize = e.ReceivedBytesSize;
        _bandwidth.CalculateSpeed(e.ProgressedByteSize);
        Options.ActiveChunks = Options.ParallelCount - ParallelSemaphore.CurrentCount;
        DownloadProgressChangedEventArgs totalProgressArg = new(nameof(DownloadService)) {
            TotalBytesToReceive = Package.TotalFileSize,
            ReceivedBytesSize = Package.ReceivedBytesSize,
            BytesPerSecondSpeed = _bandwidth.Speed,
            AverageBytesPerSecondSpeed = _bandwidth.AverageSpeed,
            ProgressedByteSize = e.ProgressedByteSize,
            ReceivedBytes = e.ReceivedBytes,
            ActiveChunks = Options.ActiveChunks
        };
        Package.SaveProgress = totalProgressArg.ProgressPercentage;
        e.ActiveChunks = totalProgressArg.ActiveChunks;
        ChunkDownloadProgressChanged?.Invoke(this, e);
        DownloadProgressChanged?.Invoke(this, totalProgressArg);
    }

    /// <summary>
    /// Raises the <see cref="ChunkDownloadProgressChanged"/> and <see cref="DownloadProgressChanged"/> events.
    /// </summary>
    /// <param name="sender">The sender of the event.</param>
    /// <param name="e">The event arguments for the download progress changed event.</param>
    protected async void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
    {
        RaiseProgressChangedEvents(e);
        await UpdatePackageAsync(e).ConfigureAwait(false);
    }

    private async Task UpdatePackageAsync(DownloadProgressChangedEventArgs e)
    {
        try
        {
            // Debounce updating package data on FileStream
            if (Package.IsFileStream && e.ProgressPercentage < 100)
            {
                if (DateTime.Now.Ticks - _lastPackageUpdateTime > 1000)
                {
                    Interlocked.Exchange(ref _lastPackageUpdateTime, DateTime.Now.Ticks);
                    byte[] pack = Serializer.Serialize(Package);
                    await Package.Storage.WriteAsync(Package.TotalFileSize, pack, pack.Length).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            Logger?.LogError(exception, exception.Message);
        }
    }

    /// <summary>
    /// Adds a logger to the download service.
    /// </summary>
    /// <param name="logger">The logger instance to add.</param>
    public void AddLogger(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Disposes of the download service, including clearing the current download operation.
    /// </summary>
    public void Dispose()
    {
        Clear().Wait();
    }

    /// <summary>
    /// Disposes asynchronously of the download service, including clearing the current download operation.
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        await Clear().ConfigureAwait(false);
    }
}