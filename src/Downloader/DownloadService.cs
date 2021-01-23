using System;
using System.ComponentModel;
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
        private readonly Bandwidth _bandwidth;
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
            Package = new DownloadPackage {
                Options = new DownloadConfiguration()
            };

            // This property selects the version of the Secure Sockets Layer (SSL) or
            // Transport Layer Security (TLS) protocol to use for new connections;
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
                Package.Options = options.Clone() as DownloadConfiguration;
            }
        }

        public async Task DownloadFileAsync(DownloadPackage package)
        {
            Package = package;
            InitialDownloader(package.Address.OriginalString);
            await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string address, string fileName)
        {
            InitialDownloader(address);
            Package.FileName = fileName;

            await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string address, DirectoryInfo folder)
        {
            InitialDownloader(address);
            var filename = await GetFilename();
            Package.FileName = Path.Combine(folder.FullName, filename);
            await StartDownload().ConfigureAwait(false);
        }

        private async Task<string> GetFilename()
        {
            string filename = await _requestInstance.GetUrlDispositionFilenameAsync();
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
            ClearChunks();

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
            _requestInstance = new Request(address, Package.Options.RequestConfiguration);
            Package.Address = _requestInstance.Address;
            _chunkHub = new ChunkHub(Package.Options);
        }

        private async Task StartDownload()
        {
            try
            {
                Package.TotalFileSize = await _requestInstance.GetFileSize();
                Validate();

                Package.Chunks = _chunkHub.ChunkFile(Package.TotalFileSize, Package.Options.ChunkCount);
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

                if (Package.Options.ParallelDownload)
                {
                    await ParallelDownload(_globalCancellationTokenSource.Token);
                }
                else
                {
                    await SerialDownload(_globalCancellationTokenSource.Token);
                }

                await CompleteDownload().ConfigureAwait(false);
            }
            catch (OperationCanceledException exp)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, true, Package));
            }
            catch (Exception exp)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(exp, false, Package));
                throw;
            }
            finally
            {
                if (IsCancelled == false)
                {
                    // remove temp files
                    ClearChunks();
                }
            }
        }

        private async Task CompleteDownload()
        {
            await _chunkHub.MergeChunks(Package.Chunks, Package.FileName).ConfigureAwait(false);
            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
        }

        private void Validate()
        {
            CheckSizes();
            Package.Options.Validate();
            if (File.Exists(Package.FileName))
            {
                File.Delete(Package.FileName);
            }
        }

        private void CheckSizes()
        {
            if (Package.TotalFileSize <= 0)
            {
                SetUnlimitedDownload();
            }

            FileHelper.CheckDiskSize(Package.FileName, Package.TotalFileSize);
            bool areTempsStoredOnDisk = Package.Options.OnTheFlyDownload == false;
            if (areTempsStoredOnDisk)
            {
                bool doubleFileSpaceNeeded = Directory.GetDirectoryRoot(Package.FileName) ==
                                             Directory.GetDirectoryRoot(Package.Options.TempDirectory);

                FileHelper.CheckDiskSize(Package.Options.TempDirectory, Package.TotalFileSize * (doubleFileSpaceNeeded ? 2 : 1));
            }
        }

        private void SetUnlimitedDownload()
        {
            Package.TotalFileSize = 0;
            Package.Options.ChunkCount = 1;
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
                await DownloadChunk(chunk, cancellationToken);
            }
        }

        private Task<Chunk> DownloadChunk(Chunk chunk, CancellationToken token)
        {
            ChunkDownloader chunkDownloader = new ChunkDownloader(chunk, Package.Options);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            return chunkDownloader.Download(_requestInstance, token);
        }

        private void ClearChunks()
        {
            if (Package.Chunks != null)
            {
                Package.ReceivedBytesSize = 0;
                foreach (Chunk chunk in Package.Chunks)
                {
                    // reset chunk for download again
                    chunk.Clear();
                    _bandwidth.Reset();
                }

                GC.Collect();
            }
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