using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : IDownloadService, IDisposable
    {
        private SemaphoreSlim _parallelSemaphore;
        private readonly SemaphoreSlim _singleInstanceSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _globalCancellationTokenSource;
        private TaskCompletionSource<AsyncCompletedEventArgs> _taskCompletion;
        private readonly PauseTokenSource _pauseTokenSource;
        private ChunkHub _chunkHub;
        private Request _requestInstance;
        private readonly Bandwidth _bandwidth;
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

        public DownloadService(DownloadConfiguration options) : this()
        {
            if (options != null)
            {
                Options = options;
            }
        }
        public DownloadService()
        {
            _pauseTokenSource = new PauseTokenSource();
            _bandwidth = new Bandwidth();
            Options = new DownloadConfiguration();
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

        public async Task<Stream> DownloadFileTaskAsync(DownloadPackage package, CancellationToken cancellationToken = default)
        {
            Package = package;
            await InitialDownloader(package.Address, cancellationToken);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task<Stream> DownloadFileTaskAsync(string address, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(address, cancellationToken);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, string fileName, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(address, cancellationToken);
            await StartDownload(fileName).ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, DirectoryInfo folder, CancellationToken cancellationToken = default)
        {
            await InitialDownloader(address, cancellationToken);
            var filename = await _requestInstance.GetFileName().ConfigureAwait(false);
            await StartDownload(Path.Combine(folder.FullName, filename)).ConfigureAwait(false);
        }

        public void Cancel()
        {
            _globalCancellationTokenSource?.Cancel(true);
            Resume();
            Status = DownloadStatus.Stopped;
        }

        public Task CancelAsync()
        {
            Cancel();
            return _taskCompletion.Task;
        }

        public void Resume()
        {
            Status = DownloadStatus.Running;
            _pauseTokenSource.Resume();
        }

        public void Pause()
        {
            _pauseTokenSource.Pause();
            Status = DownloadStatus.Paused;
        }

        public async Task Clear()
        {
            try
            {
                if (IsBusy || IsPaused)
                    Cancel();

                await _singleInstanceSemaphore?.WaitAsync();

                _parallelSemaphore?.Dispose();
                _globalCancellationTokenSource?.Dispose();
                _bandwidth.Reset();
                _requestInstance = null;

                if (_taskCompletion is not null)
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

        private async Task InitialDownloader(string address, CancellationToken cancellationToken)
        {
            await Clear();
            Status = DownloadStatus.Created;
            _globalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _taskCompletion = new TaskCompletionSource<AsyncCompletedEventArgs>();
            _requestInstance = new Request(address, Options.RequestConfiguration);
            Package.Address = _requestInstance.Address.OriginalString;
            _chunkHub = new ChunkHub(Options);
            _parallelSemaphore = new SemaphoreSlim(Options.ParallelCount, Options.ParallelCount);
        }

        private async Task StartDownload(string fileName)
        {
            Package.FileName = fileName;
            Directory.CreateDirectory(Path.GetDirectoryName(fileName)); // ensure the folder is exist
            await StartDownload().ConfigureAwait(false);
        }

        private async Task<Stream> StartDownload()
        {
            try
            {
                await _singleInstanceSemaphore.WaitAsync();
                Package.TotalFileSize = await _requestInstance.GetFileSize().ConfigureAwait(false);
                Package.IsSupportDownloadInRange = await _requestInstance.IsSupportDownloadInRange().ConfigureAwait(false);
                Package.BuildStorage(Options.ReserveStorageSpaceBeforeStartingDownload);
                ValidateBeforeChunking();
                _chunkHub.SetFileChunks(Package);

                // firing the start event after creating chunks
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

                if (Options.ParallelDownload)
                {
                    await ParallelDownload(_pauseTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    await SerialDownload(_pauseTokenSource.Token).ConfigureAwait(false);
                }

                SendDownloadCompletionSignal();
            }
            catch (OperationCanceledException exp) // or TaskCanceledException
            {
                Status = DownloadStatus.Stopped;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, true, Package));
            }
            catch (Exception exp)
            {
                Status = DownloadStatus.Failed;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, false, Package));
            }
            finally
            {
                _singleInstanceSemaphore.Release();
                await Task.Yield();
            }

            return Package.Storage?.OpenRead();
        }

        private void SendDownloadCompletionSignal()
        {
            Package.IsSaveComplete = true;
            Status = DownloadStatus.Completed;
            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
        }

        private void ValidateBeforeChunking()
        {
            CheckSingleChunkDownload();
            CheckSupportDownloadInRange();
            SetRangedSizes();
            CheckSizes();
        }

        private void SetRangedSizes()
        {
            if (Options.RangeDownload)
            {
                if (!Package.IsSupportDownloadInRange)
                {
                    throw new NotSupportedException("The server of your desired address does not support download in a specific range");
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

        private void CheckSizes()
        {
            if (Options.CheckDiskSizeBeforeDownload && !Package.InMemoryStream)
            {
                FileHelper.ThrowIfNotEnoughSpace(Package.TotalFileSize, Package.FileName);
            }
        }

        private void CheckSingleChunkDownload()
        {
            if (Package.TotalFileSize <= 1)
                Package.TotalFileSize = 0;

            if (Package.TotalFileSize <= Options.MinimumSizeOfChunking)
                SetSingleChunkDownload();
        }

        private void CheckSupportDownloadInRange()
        {
            if (Package.IsSupportDownloadInRange == false)
                SetSingleChunkDownload();
        }

        private void SetSingleChunkDownload()
        {
            Options.ChunkCount = 1;
            Options.ParallelCount = 1;
            _parallelSemaphore = new SemaphoreSlim(1, 1);
        }

        private async Task ParallelDownload(PauseToken pauseToken)
        {
            var tasks = Package.Chunks.Select(chunk => DownloadChunk(chunk, pauseToken, _globalCancellationTokenSource));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task SerialDownload(PauseToken pauseToken)
        {
            foreach (var chunk in Package.Chunks)
            {
                await DownloadChunk(chunk, pauseToken, _globalCancellationTokenSource).ConfigureAwait(false);
            }
        }

        private async Task<Chunk> DownloadChunk(Chunk chunk, PauseToken pause, CancellationTokenSource cancellationTokenSource)
        {
            ChunkDownloader chunkDownloader = new ChunkDownloader(chunk, Options, Package.Storage);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            await _parallelSemaphore.WaitAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            try
            {
                cancellationTokenSource.Token.ThrowIfCancellationRequested();
                return await chunkDownloader.Download(_requestInstance, pause, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                lock (this)
                {
                    cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    cancellationTokenSource.Cancel(true);
                    throw;
                }
            }
            finally
            {
                _parallelSemaphore.Release();
            }
        }

        private void OnDownloadStarted(DownloadStartedEventArgs e)
        {
            Status = DownloadStatus.Running;
            Package.IsSaving = true;
            DownloadStarted?.Invoke(this, e);
        }

        private void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            // flush streams
            Package.Flush();
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

        private void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
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

        public void Dispose()
        {
            Clear().Wait();
        }
    }
}