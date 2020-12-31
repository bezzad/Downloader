using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : IDownloadService, IDisposable
    {
        private const int OneSecond = 1000; // millisecond
        private ChunkProvider _chunkProvider;
        private CancellationTokenSource _globalCancellationTokenSource;
        private long _lastTickCountCheckpoint;
        private Request _requestInstance;
        private long _totalBytesReceived;

        public DownloadService(DownloadConfiguration options = null)
        {
            Package = new DownloadPackage {Options = options ?? new DownloadConfiguration()};

            ServicePointManager.SecurityProtocol =
                SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false; // accept the request for POST, PUT and PATCH verbs
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePointIdleTime = 1000;
        }

        public void Dispose()
        {
            Clear();
        }

        public bool IsBusy { get; private set; }
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
            await StartDownload();
        }

        public async Task DownloadFileAsync(string address, string fileName)
        {
            InitialBegin(address);
            Package.FileName = fileName;

            await StartDownload();
        }

        public async Task DownloadFileAsync(string address, DirectoryInfo folder)
        {
            InitialBegin(address);
            string filename = await _requestInstance.GetUrlDispositionFilenameAsync();
            if (string.IsNullOrWhiteSpace(filename))
            {
                filename = _requestInstance.GetFileName();
            }
            Package.FileName = Path.Combine(folder.FullName, filename);

            await StartDownload();
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
            _chunkProvider = Package.Options.OnTheFlyDownload
                ? (ChunkProvider)new MemoryChunkProvider(Package.Options)
                : new FileChunkProvider(Package.Options);
        }

        private async Task StartDownload()
        {
            try
            {
                Package.TotalFileSize = await _requestInstance.GetFileSize();
                Validate();

                if (File.Exists(Package.FileName))
                {
                    File.Delete(Package.FileName);
                }

                Package.Chunks = _chunkProvider.ChunkFile(Package.TotalFileSize, Package.Options.ChunkCount);
                OnDownloadStarted(new DownloadStartedEventArgs(Package.FileName, Package.TotalFileSize));

                CancellationToken cancellationToken = _globalCancellationTokenSource.Token;
                List<Task> tasks = new List<Task>();
                foreach (Chunk chunk in Package.Chunks)
                {
                    if (Package.Options.ParallelDownload)
                    {
                        Task<Chunk> task = DownloadChunk(chunk, cancellationToken);
                        tasks.Add(task);
                    }
                    else
                    {
                        await DownloadChunk(chunk, cancellationToken);
                    }
                }

                if (Package.Options.ParallelDownload && cancellationToken.IsCancellationRequested == false)
                {
                    Task.WaitAll(tasks.ToArray(), cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, true, Package));
                    return;
                }

                // Merge data to single file
                await _chunkProvider.MergeChunks(Package.Chunks, Package.FileName);

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
                if (_globalCancellationTokenSource.Token.IsCancellationRequested == false)
                {
                    // remove temp files
                    ClearChunks();
                }
            }
        }

        private void Validate()
        {
            int minNeededParts =
                (int)Math.Ceiling((double)Package.TotalFileSize / int.MaxValue); // for files as larger than 2GB
            Package.Options.ChunkCount = Package.Options.ChunkCount < minNeededParts
                ? minNeededParts
                : Package.Options.ChunkCount;
            Package.Options.Validate();
            CheckSizes();
        }

        private void CheckSizes()
        {
            if (Package.TotalFileSize <= 0)
            {
                throw new InvalidDataException("File size is invalid!");
            }

            CheckDiskSize(Package.FileName, Package.TotalFileSize);
            bool areTempsStoredOnDisk = Package.Options.OnTheFlyDownload == false;
            if (areTempsStoredOnDisk)
            {
                bool doubleFileSpaceNeeded = Directory.GetDirectoryRoot(Package.FileName) ==
                                             Directory.GetDirectoryRoot(Package.Options.TempDirectory);

                CheckDiskSize(Package.Options.TempDirectory, Package.TotalFileSize * (doubleFileSpaceNeeded ? 2 : 1));
            }
        }

        private void CheckDiskSize(string directory, long actualSize)
        {
            DriveInfo drive = new DriveInfo(Directory.GetDirectoryRoot(directory));
            if (drive.IsReady && actualSize >= drive.AvailableFreeSpace)
            {
                throw new IOException($"There is not enough space on the disk `{drive.Name}`");
            }
        }

        private async Task<Chunk> DownloadChunk(Chunk chunk, CancellationToken token)
        {
            ChunkDownloader chunkDownloader = _chunkProvider.GetChunkDownloader(chunk);
            chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
            await chunkDownloader.Download(_requestInstance, Package.Options.MaximumSpeedPerChunk, token);

            return chunk;
        }

        protected void ClearChunks()
        {
            if (Package.Options.ClearPackageAfterDownloadCompleted && Package.Chunks != null)
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
            Package.BytesReceived += e.ProgressedByteSize;
            CalculateDownloadSpeed();
            ChunkDownloadProgressChanged?.Invoke(this, e);
            DownloadProgressChanged?.Invoke(this,
                new DownloadProgressChangedEventArgs(nameof(DownloadService)) {
                    TotalBytesToReceive = Package.TotalFileSize,
                    BytesReceived = Package.BytesReceived,
                    BytesPerSecondSpeed = DownloadSpeed
                });
        }

        private void CalculateDownloadSpeed()
        {
            long duration = (Environment.TickCount - _lastTickCountCheckpoint) + 1;
            if (duration < OneSecond)
            {
                return;
            }

            long newReceivedBytes = Package.BytesReceived - _totalBytesReceived;
            DownloadSpeed = (newReceivedBytes * OneSecond) / duration; // bytes per second
            _lastTickCountCheckpoint = Environment.TickCount;
            _totalBytesReceived = Package.BytesReceived;
        }
    }
}