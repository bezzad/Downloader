using Downloader.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public abstract class AbstractDownloadService : IDownloadService, IDisposable
    {
        protected ILogger _logger;
        protected SemaphoreSlim _parallelSemaphore;
        protected readonly SemaphoreSlim _singleInstanceSemaphore = new SemaphoreSlim(1, 1);
        protected CancellationTokenSource _globalCancellationTokenSource;
        protected TaskCompletionSource<AsyncCompletedEventArgs> _taskCompletion;
        protected readonly PauseTokenSource _pauseTokenSource;
        protected ChunkHub _chunkHub;
        protected List<Request> _requestInstances;
        protected readonly Bandwidth _bandwidth;
        protected DownloadConfiguration Options { get; set; }

        public bool IsBusy => Status == DownloadStatus.Running;
        public bool IsCancelled => _globalCancellationTokenSource?.IsCancellationRequested == true;
        public bool IsPaused => _pauseTokenSource.IsPaused;
        public DownloadPackage Package { get; set; }
        public DownloadStatus Status
        {
            get => Package?.Status ?? DownloadStatus.None;
            set => Package.Status = value;
        }
        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        public event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        public AbstractDownloadService(DownloadConfiguration options)
        {
            _pauseTokenSource = new PauseTokenSource();
            _bandwidth = new Bandwidth();
            Options = options ?? new DownloadConfiguration();
            Package = new DownloadPackage();

            // This property selects the version of the Secure Sockets Layer (SSL) or
            // existing connections aren't changed.
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            // Accept the request for POST, PUT and PATCH verbs
            ServicePointManager.Expect100Continue = false;

            // Note: Any changes to the DefaultConnectionLimit property affect both HTTP 1.0 and HTTP 1.1 connections.
            // It is not possible to separately alter the connection limit for HTTP 1.0 and HTTP 1.1 protocols.
            ServicePointManager.DefaultConnectionLimit = 1000;

            // Set the maximum idle time of a ServicePoint instance to 10 seconds.
            // After the idle time expires, the ServicePoint object is eligible for
            // garbage collection and cannot be used by the ServicePointManager object.
            ServicePointManager.MaxServicePointIdleTime = 10000;

            ServicePointManager.ServerCertificateValidationCallback =
                new RemoteCertificateValidationCallback(ExceptionHelper.CertificateValidationCallBack);
        }

        public Task<Stream> DownloadFileTaskAsync(DownloadPackage package, CancellationToken cancellationToken = default)
        {
            return DownloadFileTaskAsync(package, package.Urls, cancellationToken);
        }

        public Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string address, CancellationToken cancellationToken = default)
        {
            return DownloadFileTaskAsync(package, new[] { address }, cancellationToken);
        }

        public virtual async Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string[] urls, CancellationToken cancellationToken = default)
        {
            Package = package;
            await InitialDownloader(cancellationToken, urls);
            return await StartDownload().ConfigureAwait(false);
        }

        public Task<Stream> DownloadFileTaskAsync(string address, CancellationToken cancellationToken = default)
        {
            return DownloadFileTaskAsync(new[] { address }, cancellationToken);
        }

        public virtual async Task<Stream> DownloadFileTaskAsync(string[] urls, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(cancellationToken, urls);
            return await StartDownload().ConfigureAwait(false);
        }

        public Task DownloadFileTaskAsync(string address, string fileName, CancellationToken cancellationToken = default)
        {
            return DownloadFileTaskAsync(new[] { address }, fileName, cancellationToken);
        }

        public virtual async Task DownloadFileTaskAsync(string[] urls, string fileName, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(cancellationToken, urls);
            await StartDownload(fileName).ConfigureAwait(false);
        }

        public Task DownloadFileTaskAsync(string address, DirectoryInfo folder, CancellationToken cancellationToken = default)
        {
            return DownloadFileTaskAsync(new[] { address }, folder, cancellationToken);
        }
        public virtual async Task DownloadFileTaskAsync(string[] urls, DirectoryInfo folder, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(cancellationToken, urls);
            var name = await _requestInstances.First().GetFileName().ConfigureAwait(false);
            var filename = Path.Combine(folder.FullName, name);
            await StartDownload(filename).ConfigureAwait(false);
        }

        public virtual void CancelAsync()
        {
            _globalCancellationTokenSource?.Cancel(true);
            Resume();
            Status = DownloadStatus.Stopped;
        }

        public virtual async Task CancelTaskAsync()
        {
            CancelAsync();
            if (_taskCompletion != null)
                await _taskCompletion.Task.ConfigureAwait(false);
        }

        public virtual void Resume()
        {
            Status = DownloadStatus.Running;
            _pauseTokenSource.Resume();
        }

        public virtual void Pause()
        {
            _pauseTokenSource.Pause();
            Status = DownloadStatus.Paused;
        }

        public virtual async Task Clear()
        {
            try
            {
                if (IsBusy || IsPaused)
                    await CancelTaskAsync().ConfigureAwait(false);

                await _singleInstanceSemaphore?.WaitAsync();

                _parallelSemaphore?.Dispose();
                _globalCancellationTokenSource?.Dispose();
                _bandwidth.Reset();
                _requestInstances = null;

                if (_taskCompletion != null)
                {
                    if (_taskCompletion.Task.IsCompleted == false)
                        _taskCompletion.TrySetCanceled();

                    _taskCompletion = null;
                }
                // Note: don't clear package from `DownloadService.Dispose()`.
                // Because maybe it will be used at another time.
            }
            finally
            {
                _singleInstanceSemaphore?.Release();
            }
        }

        protected async Task InitialDownloader(CancellationToken cancellationToken, params string[] addresses)
        {
            await Clear();
            Status = DownloadStatus.Created;
            _globalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _taskCompletion = new TaskCompletionSource<AsyncCompletedEventArgs>();
            _requestInstances = addresses.Select(url => new Request(url, Options.RequestConfiguration)).ToList();
            Package.Urls = _requestInstances.Select(req => req.Address.OriginalString).ToArray();
            _chunkHub = new ChunkHub(Options);
            _parallelSemaphore = new SemaphoreSlim(Options.ParallelCount, Options.ParallelCount);
        }

        protected async Task StartDownload(string fileName)
        {
            Package.FileName = fileName;
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)); // ensure the folder is exist

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            await StartDownload().ConfigureAwait(false);
        }

        protected abstract Task<Stream> StartDownload();

        protected void OnDownloadStarted(DownloadStartedEventArgs e)
        {
            Status = DownloadStatus.Running;
            Package.IsSaving = true;
            DownloadStarted?.Invoke(this, e);
        }

        protected void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            Package.IsSaving = false;

            if (e.Cancelled)
            {
                Status = DownloadStatus.Stopped;
            }
            else if (e.Error != null)
            {
                if (Options.ClearPackageOnCompletionWithFailure)
                {
                    Package.Storage?.Dispose();
                    Package.Storage = null;
                    Package.Clear();
                    if (Package.InMemoryStream == false)
                        File.Delete(Package.FileName);
                }
            }
            else // completed
            {
                Package.Clear();
            }

            if (Package.InMemoryStream == false)
            {
                Package.Storage?.Dispose();
                Package.Storage = null;
            }

            _taskCompletion.TrySetResult(e);
            DownloadFileCompleted?.Invoke(this, e);
        }

        protected void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (e.ReceivedBytesSize > Package.TotalFileSize)
                Package.TotalFileSize = e.ReceivedBytesSize;

            _bandwidth.CalculateSpeed(e.ProgressedByteSize);
            var totalProgressArg = new DownloadProgressChangedEventArgs(nameof(DownloadService)) {
                TotalBytesToReceive = Package.TotalFileSize,
                ReceivedBytesSize = Package.ReceivedBytesSize,
                BytesPerSecondSpeed = _bandwidth.Speed,
                AverageBytesPerSecondSpeed = _bandwidth.AverageSpeed,
                ProgressedByteSize = e.ProgressedByteSize,
                ReceivedBytes = e.ReceivedBytes,
                ActiveChunks = Options.ParallelCount - _parallelSemaphore.CurrentCount,
            };
            Package.SaveProgress = totalProgressArg.ProgressPercentage;
            e.ActiveChunks = totalProgressArg.ActiveChunks;
            ChunkDownloadProgressChanged?.Invoke(this, e);
            DownloadProgressChanged?.Invoke(this, totalProgressArg);
        }

        public void AddLogger(ILogger logger)
        {
            _logger = logger;
        }

        public virtual void Dispose()
        {
            Clear().Wait();
        }
    }
}