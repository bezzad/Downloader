using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : IDownloadService, IDisposable
    {
        private ChunkHub _chunkHub;
        private CancellationTokenSource _globalCancellationTokenSource;
        private Request _requestInstance;
        private Stream _destinationStream;
        private readonly Bandwidth _bandwidth;
        protected DownloadConfiguration Options { get; set; }
        public bool IsBusy { get; private set; }
        public bool IsCancelled => _globalCancellationTokenSource?.IsCancellationRequested == true;
        public DownloadPackage Package { get; set; }
        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        public event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        public DownloadService()
        {
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
        }

        public DownloadService(DownloadConfiguration options) : this()
        {
            if (options != null)
            {
                Options = options.Clone() as DownloadConfiguration;
            }
        }

        public async Task<Stream> DownloadFileTaskAsync(DownloadPackage package)
        {
            Package = package;
            InitialDownloader(package.Address);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task<Stream> DownloadFileTaskAsync(string address)
        {
            InitialDownloader(address);
            return await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, string fileName)
        {
            InitialDownloader(address);
            await StartDownload(fileName).ConfigureAwait(false);
        }

        public async Task DownloadFileTaskAsync(string address, DirectoryInfo folder)
        {
            InitialDownloader(address);
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
        }

        public void Clear()
        {
            _globalCancellationTokenSource?.Dispose();
            _globalCancellationTokenSource = new CancellationTokenSource();
            _bandwidth.Reset();
            Package.Clear();

            Package.FileName = null;
            Package.TotalFileSize = 0;
            Package.ReceivedBytesSize = 0;
            Package.Chunks = null;
            _requestInstance = null;
            IsBusy = false;
        }

        private void InitialDownloader(string address)
        {
            IsBusy = true;
            _globalCancellationTokenSource = new CancellationTokenSource();
            _requestInstance = new Request(address, Options.RequestConfiguration);
            Package.Address = _requestInstance.Address.OriginalString;
            _chunkHub = new ChunkHub(Options);
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
                Package.TotalFileSize = await _requestInstance.GetFileSize().ConfigureAwait(false);
                Validate();
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));
                Package.Chunks ??= _chunkHub.ChunkFile(Package.TotalFileSize, Options.ChunkCount);

                if (Options.ParallelDownload)
                {
                    await ParallelDownload(_globalCancellationTokenSource.Token).ConfigureAwait(false);
                }
                else
                {
                    await SerialDownload(_globalCancellationTokenSource.Token).ConfigureAwait(false);
                }

                await StoreDownloadedFile(_globalCancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException exp)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, true, Package));
            }
            catch (Exception exp)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, false, Package));
                Debugger.Break();
                throw;
            }
            finally
            {
                if (IsCancelled)
                {
                    // flush streams
                    Package.Flush();
                }
                else
                {
                    // remove temp files
                    Package.Clear();
                }
            }

            return _destinationStream;
        }

        private async Task StoreDownloadedFile(CancellationToken cancellationToken)
        {
            try
            {
                _destinationStream = Package.FileName == null
                    ? new MemoryStream()
                    : FileHelper.CreateFile(Package.FileName);
                await _chunkHub.MergeChunks(Package.Chunks, _destinationStream, cancellationToken).ConfigureAwait(false);
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
            }
            finally
            {
                var isStoreOnMemory = Package?.FileName == null;
                if (isStoreOnMemory == false)
                {
                    _destinationStream?.Dispose();
                }
            }
        }

        private void Validate()
        {
            CheckUnlimitedDownload();
            CheckSizes();
            if (File.Exists(Package.FileName))
            {
                File.Delete(Package.FileName);
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
            if (Package.TotalFileSize <= 0)
            {
                Package.TotalFileSize = 0;
                Options.ChunkCount = 1;
            }
        }

        private async Task ParallelDownload(CancellationToken cancellationToken)
        {
            var tasks = Package.Chunks.Select(chunk => DownloadChunk(chunk, cancellationToken));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private async Task SerialDownload(CancellationToken cancellationToken)
        {
            foreach (var chunk in Package.Chunks)
            {
                await DownloadChunk(chunk, cancellationToken).ConfigureAwait(false);
            }
        }

        private Task<Chunk> DownloadChunk(Chunk chunk, CancellationToken cancellationToken)
        {
            ChunkDownloader chunkDownloader = new ChunkDownloader(chunk, Options);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            return chunkDownloader.Download(_requestInstance, cancellationToken);
        }

        private void OnDownloadStarted(DownloadStartedEventArgs e)
        {
            DownloadStarted?.Invoke(this, e);
        }

        private void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            IsBusy = false;
            DownloadFileCompleted?.Invoke(this, e);
        }

        private void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Package.AddReceivedBytes(e.ProgressedByteSize);
            _bandwidth.CalculateSpeed(e.ProgressedByteSize);

            ChunkDownloadProgressChanged?.Invoke(this, e);
            DownloadProgressChanged?.Invoke(this,
                new DownloadProgressChangedEventArgs(nameof(DownloadService)) {
                    TotalBytesToReceive = Package.TotalFileSize,
                    ReceivedBytesSize = Package.ReceivedBytesSize,
                    BytesPerSecondSpeed = _bandwidth.Speed,
                    AverageBytesPerSecondSpeed = _bandwidth.AverageSpeed,
                    ReceivedBytes = e.ReceivedBytes
                });
        }

        public void Dispose()
        {
            Clear();
        }
    }
}