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
        private const int OneSecond = 1000; // millisecond
        private ChunkHub _chunkHub;
        private CancellationTokenSource _globalCancellationTokenSource;
        private int _lastTickCountCheckpoint;
        private Request _requestInstance;
        private long _totalBytesReceived;

        public DownloadService()
        {
            Package = new DownloadPackage {
                Options = new DownloadConfiguration()
            };

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false; // accept the request for POST, PUT and PATCH verbs
            ServicePointManager.DefaultConnectionLimit = Math.Max(Package.Options.ChunkCount, ServicePointManager.DefaultPersistentConnectionLimit);
            ServicePointManager.MaxServicePointIdleTime = 1000;
        }

        public DownloadService(DownloadConfiguration options) : this()
        {
            if (options != null)
            {
                Package.Options = options.Clone() as DownloadConfiguration;
            }
        }

        public void Dispose()
        {
            Clear();
        }

        public bool IsBusy { get; private set; }
        public bool IsCancelled => _globalCancellationTokenSource?.IsCancellationRequested == true;
        public long DownloadSpeed { get; private set; }
        public DownloadPackage Package { get; set; }
        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        public event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        public async Task DownloadFileAsync(DownloadPackage package)
        {
            Package = package;
            InitialBegin(package.Address.OriginalString);
            await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string address, string fileName)
        {
            InitialBegin(address);
            Package.FileName = fileName;

            await StartDownload().ConfigureAwait(false);
        }

        public async Task DownloadFileAsync(string address, DirectoryInfo folder)
        {
            InitialBegin(address);
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
            Package.BytesReceived = 0;
            Package.Chunks = null;
            _requestInstance = null;
            IsBusy = false;
        }

        private void InitialBegin(string address)
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

                await _chunkHub.MergeChunks(Package.Chunks, Package.FileName).ConfigureAwait(false);
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, Package));
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
                Package.BytesReceived = 0;
                foreach (Chunk chunk in Package.Chunks)
                {
                    // reset chunk for download again
                    chunk.Clear();
                    _totalBytesReceived = 0;
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
            lock (this)
            {
                Package.BytesReceived += e.ProgressedByteSize;
                CalculateDownloadSpeed();
                ChunkDownloadProgressChanged?.Invoke(this, e);
                DownloadProgressChanged?.Invoke(this,
                    new DownloadProgressChangedEventArgs(nameof(DownloadService)) {
                        TotalBytesToReceive = Package.TotalFileSize,
                        ReceivedBytesSize = Package.BytesReceived,
                        BytesPerSecondSpeed = DownloadSpeed
                    });
            }
        }

        private void CalculateDownloadSpeed()
        {
            int duration = Environment.TickCount - _lastTickCountCheckpoint + 1;
            if (duration >= OneSecond)
            {
                long newReceivedBytes = Package.BytesReceived - _totalBytesReceived;
                DownloadSpeed = (newReceivedBytes * OneSecond) / duration; // bytes per second
                _lastTickCountCheckpoint = Environment.TickCount;
                _totalBytesReceived = Package.BytesReceived;
            }
        }
    }
}