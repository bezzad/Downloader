using System;
using System.Collections.Generic;
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
        public DownloadService(DownloadConfiguration options = null)
        {
            Package = new DownloadPackage() {
                Options = options ?? new DownloadConfiguration()
            };

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false; // accept the request for POST, PUT and PATCH verbs
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePointIdleTime = 1000;
        }

        protected long TotalBytesReceived { get; set; }
        protected long LastTickCountCheckpoint { get; set; }
        protected const int OneSecond = 1000; // millisecond
        protected Request RequestInstance { get; set; }
        protected CancellationTokenSource GlobalCancellationTokenSource { get; set; }
        public bool IsBusy { get; protected set; }
        public long DownloadSpeed { get; protected set; }
        public DownloadPackage Package { get; set; }
        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;

        public async Task DownloadFileAsync(DownloadPackage package)
        {
            Package = package;
            InitialBegin(package.Address.OriginalString);
            await StartDownload();
        }
        public async Task DownloadFileAsync(string address, DirectoryInfo folder)
        {
            InitialBegin(address);
            folder.Create();
            var filename = await RequestInstance.GetUrlDispositionFilenameAsync() ?? RequestInstance.GetFileName();
            Package.FileName = Path.Combine(folder.FullName, filename);

            await StartDownload();
        }
        public async Task DownloadFileAsync(string address, string fileName)
        {
            InitialBegin(address);
            Package.FileName = fileName;

            await StartDownload();
        }
        protected void InitialBegin(string address)
        {
            IsBusy = true;
            GlobalCancellationTokenSource = new CancellationTokenSource();
            RequestInstance = new Request(address, Package.Options.RequestConfiguration);
            Package.Address = RequestInstance.Address;
        }
        protected async Task StartDownload()
        {
            try
            {
                Package.TotalFileSize = await RequestInstance.GetFileSize();
                Validate();
                CheckSizes();

                if (File.Exists(Package.FileName))
                    File.Delete(Package.FileName);

                Package.Chunks = ChunkFile(Package.TotalFileSize, Package.Options.ChunkCount);
                var cancellationToken = GlobalCancellationTokenSource.Token;
                var tasks = new List<Task>();
                foreach (var chunk in Package.Chunks)
                {
                    if (Package.Options.ParallelDownload)
                    {
                        // download as parallel
                        var task = DownloadChunk(chunk, cancellationToken);
                        tasks.Add(task);
                    }
                    else
                    {
                        // download as async and serial
                        await DownloadChunk(chunk, cancellationToken);
                    }
                }

                if (Package.Options.ParallelDownload && cancellationToken.IsCancellationRequested == false
                ) // is parallel
                    Task.WaitAll(tasks.ToArray(), cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, true, Package));
                    return;
                }

                // Merge data to single file
                await MergeChunks(Package.Chunks);

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
                if (GlobalCancellationTokenSource.Token.IsCancellationRequested == false)
                {
                    // remove temp files
                    ClearChunks();
                }
            }
        }
        protected void Validate()
        {
            var minNeededParts = (int)Math.Ceiling((double)Package.TotalFileSize / int.MaxValue); // for files as larger than 2GB
            Package.Options.ChunkCount = Package.Options.ChunkCount < minNeededParts ? minNeededParts : Package.Options.ChunkCount;
            Package.Options.Validate();
        }
        protected void CheckSizes()
        {
            if (Package.TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            CheckDiskSize(Package.FileName, Package.TotalFileSize);
            var areTempsStoredOnDisk = Package.Options.OnTheFlyDownload == false;
            if (areTempsStoredOnDisk)
            {
                var doubleFileSpaceNeeded = Directory.GetDirectoryRoot(Package.FileName) ==
                                            Directory.GetDirectoryRoot(Package.Options.TempDirectory);

                CheckDiskSize(Package.Options.TempDirectory, Package.TotalFileSize * (doubleFileSpaceNeeded ? 2 : 1));
            }
        }
        protected void CheckDiskSize(string directory, long actualSize)
        {
            var drive = new DriveInfo(Directory.GetDirectoryRoot(directory));
            if (drive.IsReady && actualSize >= drive.AvailableFreeSpace)
                throw new IOException($"There is not enough space on the disk `{drive.Name}`");
        }
        protected Chunk[] ChunkFile(long fileSize, int parts)
        {
            if (parts < 1)
                parts = 1;
            var chunkSize = fileSize / parts;

            if (chunkSize < 1)
            {
                chunkSize = 1;
                parts = (int)fileSize;
            }

            var chunks = new Chunk[parts];
            for (var i = 0; i < parts; i++)
            {
                var isLastChunk = i == parts - 1;
                var startPosition = i * chunkSize;
                var endPosition = isLastChunk ? fileSize - 1 : startPosition + chunkSize - 1;
                Chunk chunk = Package.Options.OnTheFlyDownload
                    ? (Chunk)new MemoryChunk(startPosition, endPosition)
                    : new FileChunk(startPosition, endPosition);
                chunk.MaxTryAgainOnFailover = Package.Options.MaxTryAgainOnFailover;
                chunk.Timeout = Package.Options.Timeout;
                chunks[i] = chunk;
            }

            return chunks;
        }
        protected async Task<Chunk> DownloadChunk(Chunk chunk, CancellationToken token)
        {
            if (Package.Options.OnTheFlyDownload && chunk is MemoryChunk memoryChunk)
            {
                var chunkDownloader = new MemoryChunkDownloader(memoryChunk, Package.Options.BufferBlockSize);
                chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
                await chunkDownloader.Download(RequestInstance, Package.Options.MaximumSpeedPerChunk, token);
            }
            else if (chunk is FileChunk fileChunk)
            {
                var chunkDownloader = new FileChunkDownloader(fileChunk, Package.Options.BufferBlockSize,
                    Package.Options.TempDirectory, Package.Options.TempFilesExtension);
                chunkDownloader.DownloadProgressChanged += OnChunkDownloadProgressChanged;
                await chunkDownloader.Download(RequestInstance, Package.Options.MaximumSpeedPerChunk, token);
            }

            return chunk;
        }
        protected async Task MergeChunks(Chunk[] chunks)
        {
            var directory = Path.GetDirectoryName(Package.FileName);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            using var destinationStream = new FileStream(Package.FileName, FileMode.Append, FileAccess.Write);
            foreach (var chunk in chunks.OrderBy(c => c.Start))
            {
                if (Package.Options.OnTheFlyDownload && chunk is MemoryChunk memoryChunk)
                {
                    await destinationStream.WriteAsync(memoryChunk.Data, 0, (int)chunk.Length);
                }
                else if (chunk is FileChunk fileChunk && File.Exists(fileChunk.FileName))
                {
                    using var reader = File.OpenRead(fileChunk.FileName);
                    await reader.CopyToAsync(destinationStream);
                }
            }
        }
        protected void ClearChunks()
        {
            if (Package.Options.ClearPackageAfterDownloadCompleted && Package.Chunks != null)
            {
                Package.BytesReceived = 0;
                foreach (var chunk in Package.Chunks)
                {
                    // reset chunk for download again
                    chunk.Clear();
                    TotalBytesReceived = 0;
                }
                GC.Collect();
            }
        }
        protected virtual void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            IsBusy = false;
            DownloadFileCompleted?.Invoke(this, e);
        }
        protected virtual void OnChunkDownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Package.BytesReceived += e.ProgressedByteSize;
            CalculateDownloadSpeed();
            ChunkDownloadProgressChanged?.Invoke(this, e);
            DownloadProgressChanged?.Invoke(this, new DownloadProgressChangedEventArgs(nameof(DownloadService)) {
                TotalBytesToReceive = Package.TotalFileSize,
                BytesReceived = Package.BytesReceived,
                BytesPerSecondSpeed = DownloadSpeed
            });
        }
        protected virtual void CalculateDownloadSpeed()
        {
            var duration = Environment.TickCount - LastTickCountCheckpoint + 1;
            if (duration < OneSecond)
                return;
            var newReceivedBytes = Package.BytesReceived - TotalBytesReceived;
            DownloadSpeed = newReceivedBytes * OneSecond / duration; // bytes per second
            LastTickCountCheckpoint = Environment.TickCount;
            TotalBytesReceived = Package.BytesReceived;
        }
        public void CancelAsync()
        {
            GlobalCancellationTokenSource?.Cancel(false);
        }
        public void Dispose()
        {
            Clear();
        }
        public void Clear()
        {
            GlobalCancellationTokenSource?.Dispose();
            GlobalCancellationTokenSource = new CancellationTokenSource();
            ClearChunks();

            Package.FileName = null;
            Package.TotalFileSize = 0;
            Package.BytesReceived = 0;
            Package.Chunks = null;
            RequestInstance = null;
            IsBusy = false;
        }
    }
}