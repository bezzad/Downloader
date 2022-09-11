using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : IDownloadService, IDisposable
    {
        private SemaphoreSlim _parallelSemaphore;
        private SemaphoreSlim _singleInstanceSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _globalCancellationTokenSource;
        private PauseTokenSource _pauseTokenSource;
        private ChunkHub _chunkHub;
        private Request _requestInstance;
        private Stream _destinationStream;
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

        // ReSharper disable once MemberCanBePrivate.Global
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

            ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CertificateValidationCallBack);
        }

        /// <summary>
        /// Sometime a server get certificate validation error
        /// https://stackoverflow.com/questions/777607/the-remote-certificate-is-invalid-according-to-the-validation-procedure-using
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="certificate"></param>
        /// <param name="chain"></param>
        /// <param name="sslPolicyErrors"></param>
        /// <returns></returns>
        private static bool CertificateValidationCallBack(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // If there are errors in the certificate chain, look at each error to determine the cause.
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                if (chain?.ChainStatus is not null)
                {
                    foreach (X509ChainStatus status in chain.ChainStatus)
                    {
                        if (status.Status == X509ChainStatusFlags.NotTimeValid)
                        {
                            // If the error is for certificate expiration then it can be continued
                            return true;
                        }
                        else if ((certificate.Subject == certificate.Issuer) &&
                                 (status.Status == X509ChainStatusFlags.UntrustedRoot))
                        {
                            // Self-signed certificates with an untrusted root are valid. 
                            continue;
                        }
                        else if (status.Status != X509ChainStatusFlags.NoError)
                        {
                            // If there are any other errors in the certificate chain, the certificate is invalid,
                            // so the method returns false.
                            return false;
                        }
                    }
                }

                // When processing reaches this line, the only errors in the certificate chain are 
                // untrusted root errors for self-signed certificates. These certificates are valid
                // for default Exchange server installations, so return true.
                return true;
            }
            else
            {
                // In all other cases, return false.
                return false;
            }
        }

        public DownloadService(DownloadConfiguration options) : this()
        {
            if (options != null)
            {
                Options = options;
            }
        }

        public async Task<Stream> DownloadFileTaskAsync(DownloadPackage package)
        {
            Package = package;
            await InitialDownloader(package.Address);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task<Stream> DownloadFileTaskAsync(string address)
        {
            await InitialDownloader(address);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, string fileName)
        {
            await InitialDownloader(address);
            await StartDownload(fileName).ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, DirectoryInfo folder)
        {
            await InitialDownloader(address);
            var filename = await GetFilename().ConfigureAwait(false);
            await StartDownload(Path.Combine(folder.FullName, filename)).ConfigureAwait(false);
        }

        private async Task<string> GetFilename()
        {
            string filename = await _requestInstance.GetUrlDispositionFilenameAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = _requestInstance.GetFileName();
                if (string.IsNullOrWhiteSpace(filename))
                {
                    filename = Guid.NewGuid().ToString("N");
                }
            }

            return filename;
        }

        public void CancelAsync()
        {
            _globalCancellationTokenSource?.Cancel(false);
            Resume();
            Status = DownloadStatus.Stopped;
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
                    CancelAsync();

                await _singleInstanceSemaphore?.WaitAsync();

                _parallelSemaphore?.Dispose();
                _globalCancellationTokenSource?.Dispose();
                _globalCancellationTokenSource = new CancellationTokenSource();
                _bandwidth.Reset();
                _requestInstance = null;
                // Note: don't clear package from `DownloadService.Dispose()`.
                // Because maybe it will be used at another time.
            }
            finally
            {
                _singleInstanceSemaphore?.Release();
            }
        }

        private async Task InitialDownloader(string address)
        {
            await Clear();
            Status = DownloadStatus.Created;
            _globalCancellationTokenSource = new CancellationTokenSource();
            _requestInstance = new Request(address, Options.RequestConfiguration);
            Package.Address = _requestInstance.Address.OriginalString;
            _chunkHub = new ChunkHub(Options);
            _parallelSemaphore = new SemaphoreSlim(Options.ParallelCount, Options.ParallelCount);
        }

        private async Task StartDownload(string fileName)
        {
            Package.FileName = fileName;
            await StartDownload().ConfigureAwait(false);
        }

        private async Task<Stream> StartDownload()
        {
            try
            {
                await _singleInstanceSemaphore.WaitAsync();
                Package.TotalFileSize = await _requestInstance.GetFileSize().ConfigureAwait(false);
                Package.IsSupportDownloadInRange = await _requestInstance.IsSupportDownloadInRange().ConfigureAwait(false);
                ValidateBeforeChunking();
                Package.Chunks ??= _chunkHub.ChunkFile(Package.TotalFileSize, Options.ChunkCount, Options.RangeLow);
                Package.Validate();

                // firing the start event after creating chunks
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

                if (Options.ParallelDownload)
                {
                    await ParallelDownload(_pauseTokenSource.Token, _globalCancellationTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    await SerialDownload(_pauseTokenSource.Token, _globalCancellationTokenSource.Token).ConfigureAwait(false);
                }

                await StoreDownloadedFile(_globalCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exp)
            {
                Status = DownloadStatus.Stopped;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, true, Package));
            }
            catch (Exception exp)
            {
                Status = DownloadStatus.Failed;
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, false, Package));
                Debugger.Break();
            }
            finally
            {
                if (IsCancelled || Status == DownloadStatus.Stopped)
                {
                    Status = DownloadStatus.Stopped;
                    // flush streams
                    Package.Flush();
                }
                else if (Package.IsSaveComplete || Options.ClearPackageOnCompletionWithFailure)
                {
                    // remove temp files
                    Package.Clear();
                }

                _singleInstanceSemaphore.Release();
                await Task.Yield();
            }

            return _destinationStream;
        }

        private async Task StoreDownloadedFile(CancellationToken cancellationToken)
        {
            _destinationStream = Package.FileName == null
                ? new MemoryStream()
                : FileHelper.CreateFile(Package.FileName);
            await _chunkHub.MergeChunks(Package.Chunks, _destinationStream, cancellationToken).ConfigureAwait(false);

            if (_destinationStream is FileStream)
            {
                _destinationStream?.Dispose();
            }

            Package.IsSaveComplete = true;
            Status = DownloadStatus.Completed;
            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
        }

        private void ValidateBeforeChunking()
        {
            CheckUnlimitedDownload();
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
            if (Options.CheckDiskSizeBeforeDownload)
            {
                if (Options.OnTheFlyDownload)
                {
                    FileHelper.ThrowIfNotEnoughSpace(Package.TotalFileSize, Package.FileName);
                }
                else
                {
                    FileHelper.ThrowIfNotEnoughSpace(Package.TotalFileSize, Package.FileName, Options.TempDirectory);
                }
            }
        }

        private void CheckUnlimitedDownload()
        {
            if (Package.TotalFileSize <= 1)
            {
                Package.IsSupportDownloadInRange = false;
                Package.TotalFileSize = 0;
            }
        }

        private void CheckSupportDownloadInRange()
        {
            if (Package.IsSupportDownloadInRange == false)
            {
                Options.ChunkCount = 1;
                Options.ParallelCount = 1;
                _parallelSemaphore = new SemaphoreSlim(1, 1);
            }
        }

        private async Task ParallelDownload(PauseToken pauseToken, CancellationToken cancellationToken)
        {
            var tasks = Package.Chunks.Select(chunk => DownloadChunk(chunk, pauseToken, cancellationToken));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task SerialDownload(PauseToken pauseToken, CancellationToken cancellationToken)
        {
            foreach (var chunk in Package.Chunks)
            {
                await DownloadChunk(chunk, pauseToken, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<Chunk> DownloadChunk(Chunk chunk, PauseToken pause, CancellationToken cancellationToken)
        {
            ChunkDownloader chunkDownloader = new ChunkDownloader(chunk, Options);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            await _parallelSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await chunkDownloader.Download(_requestInstance, pause, cancellationToken).ConfigureAwait(false);
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
            Package.IsSaving = false;
            DownloadFileCompleted?.Invoke(this, e);
        }

        private void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
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
            ChunkDownloadProgressChanged?.Invoke(this, e);
            DownloadProgressChanged?.Invoke(this, totalProgressArg);
        }

        public void Dispose()
        {
            Clear().Wait();
        }
    }
}