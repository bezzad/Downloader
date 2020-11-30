using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public class DownloadService : IDownloadService
    {
        public DownloadService(DownloadConfiguration options = null)
        {
            Package = new DownloadPackage()
            {
                Options = options ?? new DownloadConfiguration()
            };

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            ServicePointManager.Expect100Continue = false; // accept the request for POST, PUT and PATCH verbs
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePointIdleTime = 1000;
        }


        protected const string HeadRequestMethod = "HEAD";
        protected const string GetRequestMethod = "GET";
        protected long BytesReceivedCheckPoint { get; set; }
        protected long LastDownloadCheckpoint { get; set; }
        protected CancellationTokenSource GlobalCancellationTokenSource { get; set; }

        /// <summary>
        /// Is in downloading time
        /// </summary>
        public bool IsBusy { get; protected set; }
        public long DownloadSpeed { get; set; }
        public DownloadPackage Package { get; set; }
        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;

        public async Task DownloadFileAsync(DownloadPackage package)
        {
            IsBusy = true;
            GlobalCancellationTokenSource = new CancellationTokenSource();
            Package = package;
            Package.Options.Validate();

            CheckSizes();

            if (File.Exists(Package.FileName))
                File.Delete(Package.FileName);

            await StartDownload();
        }
        public async Task DownloadFileAsync(string address, string fileName)
        {
            try
            {
                IsBusy = true;
                GlobalCancellationTokenSource = new CancellationTokenSource();
                Package.FileName = fileName;
                Package.Address = new Uri(address); 
                Package.TotalFileSize = await GetFileSize(Package.Address, Package.Options.AllowedHeadRequest);
                Package.Options.Validate();

                CheckSizes();

                var neededParts = (int)Math.Ceiling((double)Package.TotalFileSize / int.MaxValue); // for files as larger than 2GB

                // Handle number of parallel downloads  
                var parts = Package.Options.ChunkCount < neededParts
                    ? neededParts
                    : Package.Options.ChunkCount;

                Package.Chunks = ChunkFile(Package.TotalFileSize, parts);

                if (File.Exists(Package.FileName))
                    File.Delete(Package.FileName);

                await StartDownload();
            }
            catch (Exception e)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, Package));
                throw;
            }
        }
        public void CancelAsync()
        {
            GlobalCancellationTokenSource?.Cancel(false);
        }
        public void Clear()
        {
            GlobalCancellationTokenSource?.Dispose();
            GlobalCancellationTokenSource = new CancellationTokenSource();
            ClearTemps();

            Package.FileName = null;
            Package.TotalFileSize = 0;
            Package.BytesReceived = 0;
            Package.Chunks = null;
        }
        protected void ClearTemps()
        {
            if (Package.Options.ClearPackageAfterDownloadCompleted && Package.Chunks != null)
            {
                Package.BytesReceived = 0;
                foreach (var chunk in Package.Chunks)
                {
                    if (Package.Options.OnTheFlyDownload)
                        chunk.Data = null;
                    else if (File.Exists(chunk.FileName))
                        File.Delete(chunk.FileName);

                    chunk.Position = 0; // reset position for again download
                    BytesReceivedCheckPoint = 0;
                }
                GC.Collect();
            }
        }

        protected HttpWebRequest GetRequest(string method, Uri address)
        {
            var request = (HttpWebRequest)WebRequest.CreateDefault(address);
            request.Timeout = -1;
            request.Method = method;

            request.Accept = Package.Options.RequestConfiguration.Accept;
            request.KeepAlive = Package.Options.RequestConfiguration.KeepAlive;
            request.AllowAutoRedirect = Package.Options.RequestConfiguration.AllowAutoRedirect;
            request.AutomaticDecompression = Package.Options.RequestConfiguration.AutomaticDecompression;
            request.UserAgent = Package.Options.RequestConfiguration.UserAgent;
            request.ProtocolVersion = Package.Options.RequestConfiguration.ProtocolVersion;
            request.UseDefaultCredentials = Package.Options.RequestConfiguration.UseDefaultCredentials;
            request.SendChunked = Package.Options.RequestConfiguration.SendChunked;
            request.TransferEncoding = Package.Options.RequestConfiguration.TransferEncoding;
            request.Expect = Package.Options.RequestConfiguration.Expect;
            request.MaximumAutomaticRedirections = Package.Options.RequestConfiguration.MaximumAutomaticRedirections;
            request.MediaType = Package.Options.RequestConfiguration.MediaType;
            request.PreAuthenticate = Package.Options.RequestConfiguration.PreAuthenticate;
            request.Credentials = Package.Options.RequestConfiguration.Credentials;
            request.ClientCertificates = Package.Options.RequestConfiguration.ClientCertificates;
            request.Referer = Package.Options.RequestConfiguration.Referer;
            request.Pipelined = Package.Options.RequestConfiguration.Pipelined;
            request.Proxy = Package.Options.RequestConfiguration.Proxy;

            if (Package.Options.RequestConfiguration.IfModifiedSince.HasValue)
                request.IfModifiedSince = Package.Options.RequestConfiguration.IfModifiedSince.Value;

            return request;
        }
        protected async Task<long> GetFileSize(Uri address, bool withHeadRequest = true)
        {
            //
            // Fetch file size with HEAD or GET request
            // 
            var result = -1L;
            var request = withHeadRequest ? GetRequest(HeadRequestMethod, address) : GetRequest(GetRequestMethod, address);
            try
            {
                using var response = await request.GetResponseAsync();
                if (response.SupportsHeaders)
                    result = response.ContentLength;
            }
            catch (WebException exp)
                when (exp.Response is HttpWebResponse response &&
                     (response.StatusCode == HttpStatusCode.MethodNotAllowed
                      || response.StatusCode == HttpStatusCode.Forbidden))
            {
                // ignore WebException, Request method 'HEAD' not supported from host!
                result = -1L;
            }

            if (result <= 0 && withHeadRequest)
                result = await GetFileSize(address, false);

            return result;
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
            for (var chunk = 0; chunk < parts; chunk++)
            {
                chunks[chunk] =
                    (chunk == parts - 1)
                        ? new Chunk(chunk * chunkSize, fileSize - 1) // last chunk
                        : new Chunk(chunk * chunkSize, (chunk + 1) * chunkSize - 1);
            }

            return chunks;
        }
        protected async Task StartDownload()
        {
            try
            {
                var cancellationToken = GlobalCancellationTokenSource.Token;
                var tasks = new List<Task>();
                foreach (var chunk in Package.Chunks)
                {
                    if (Package.Options.ParallelDownload)
                    {   // download as parallel
                        var task = DownloadChunk(Package.Address, chunk, cancellationToken);
                        tasks.Add(task);
                    }
                    else
                    {   // download as async and serial
                        await DownloadChunk(Package.Address, chunk, cancellationToken);
                    }
                }

                if (Package.Options.ParallelDownload && cancellationToken.IsCancellationRequested == false) // is parallel
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
            catch (OperationCanceledException e)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, true, Package));
            }
            finally
            {
                if (GlobalCancellationTokenSource.Token.IsCancellationRequested == false)
                {
                    // remove temp files
                    ClearTemps();
                }
            }
        }
        protected async Task<Chunk> DownloadChunk(Uri address, Chunk chunk, CancellationToken token)
        {
            try
            {
                if (token.IsCancellationRequested)
                    return chunk;

                var request = GetRequest(GetRequestMethod, address);
                if (chunk.Start + chunk.Position >= chunk.End && chunk.Data?.LongLength == chunk.Length)
                    return chunk; // downloaded completely before

                if (chunk.Position >= chunk.Length && chunk.Data == null)
                    chunk.Position = 0; // downloaded again and reset chunk position

                request.AddRange(chunk.Start + chunk.Position, chunk.End);

                using var httpWebResponse = request.GetResponse() as HttpWebResponse;
                if (httpWebResponse == null)
                    return chunk;

                var stream = httpWebResponse.GetResponseStream();
                var destinationStream = new ThrottledStream(stream,
                    Package.Options.ParallelDownload
                    ? Package.Options.MaximumBytesPerSecond / Package.Options.ChunkCount
                    : Package.Options.MaximumBytesPerSecond);
                using (stream)
                {
                    if (stream == null)
                        return chunk;

                    if (Package.Options.OnTheFlyDownload)
                        await ReadStreamOnTheFly(destinationStream, chunk, token);
                    else
                        await ReadStreamOnTheFile(destinationStream, chunk, token);
                }

                return chunk;
            }
            catch (TaskCanceledException) // when stream reader timeout occured 
            {
                // re-request
                if (token.IsCancellationRequested == false)
                    await DownloadChunk(address, chunk, token);
            }
            catch (WebException) when (token.IsCancellationRequested == false &&
                                       chunk.FailoverCount++ <= Package.Options.MaxTryAgainOnFailover)
            {
                // when the host forcibly closed the connection.
                await Task.Delay(Package.Options.Timeout, token);
                chunk.Checkpoint();
                // re-request
                await DownloadChunk(address, chunk, token);
            }
            catch (Exception e) when (token.IsCancellationRequested == false &&
                                     chunk.FailoverCount++ <= Package.Options.MaxTryAgainOnFailover &&
                                     (e.HasSource("System.Net.Http") ||
                                      e.HasSource("System.Net.Sockets") ||
                                      e.HasSource("System.Net.Security") ||
                                      e.InnerException is SocketException))
            {
                // wait and decrease speed to low pressure on host
                Package.Options.Timeout += chunk.CanContinue() ? 0 : 500;
                chunk.Checkpoint();
                await Task.Delay(Package.Options.Timeout, token);
                // re-request
                await DownloadChunk(address, chunk, token);
            }
            catch (Exception e) // Maybe no internet!
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, Package));
                Debugger.Break();
            }

            return chunk;
        }

        protected async Task ReadStreamOnTheFly(Stream stream, Chunk chunk, CancellationToken token)
        {
            var bytesToReceiveCount = chunk.Length - chunk.Position;
            chunk.Data ??= new byte[chunk.Length];
            while (bytesToReceiveCount > 0)
            {
                if (token.IsCancellationRequested)
                    return;

                using var innerCts = new CancellationTokenSource(Package.Options.Timeout);
                var count = bytesToReceiveCount > Package.Options.BufferBlockSize
                    ? Package.Options.BufferBlockSize
                    : (int)bytesToReceiveCount;
                var readSize = await stream.ReadAsync(chunk.Data, chunk.Position, count, innerCts.Token);
                Package.BytesReceived += readSize;
                chunk.Position += readSize;
                bytesToReceiveCount = chunk.Length - chunk.Position;

                OnChunkDownloadProgressChanged(new DownloadProgressChangedEventArgs(chunk.Id, chunk.Length, chunk.Position, DownloadSpeed));
                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(null, Package.TotalFileSize, Package.BytesReceived, DownloadSpeed));
            }
        }
        protected async Task ReadStreamOnTheFile(Stream stream, Chunk chunk, CancellationToken token)
        {
            var bytesToReceiveCount = chunk.Length - chunk.Position;
            if (string.IsNullOrWhiteSpace(chunk.FileName) || File.Exists(chunk.FileName) == false)
                chunk.FileName = Package.Options.TempDirectory.GetTempFile(Package.Options.TempFilesExtension);

            using var writer = new FileStream(chunk.FileName, FileMode.Append, FileAccess.Write, FileShare.Delete);
            while (bytesToReceiveCount > 0)
            {
                if (token.IsCancellationRequested)
                    return;

                using var innerCts = new CancellationTokenSource(Package.Options.Timeout);
                var count = bytesToReceiveCount > Package.Options.BufferBlockSize
                    ? Package.Options.BufferBlockSize
                    : (int)bytesToReceiveCount;
                var buffer = new byte[count];
                var readSize = await stream.ReadAsync(buffer, 0, count, innerCts.Token);
                // ReSharper disable once MethodSupportsCancellation
                await writer.WriteAsync(buffer, 0, readSize);
                Package.BytesReceived += readSize;
                chunk.Position += readSize;
                bytesToReceiveCount = chunk.Length - chunk.Position;

                OnChunkDownloadProgressChanged(new DownloadProgressChangedEventArgs(chunk.Id, chunk.Length, chunk.Position, DownloadSpeed));
                OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(null, Package.TotalFileSize, Package.BytesReceived, DownloadSpeed));
            }
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
                if (Package.Options.OnTheFlyDownload)
                {
                    await destinationStream.WriteAsync(chunk.Data, 0, (int)chunk.Length);
                }
                else if (File.Exists(chunk.FileName))
                {
                    using var reader = File.OpenRead(chunk.FileName);
                    await reader.CopyToAsync(destinationStream);
                }
            }
        }

        protected void CheckSizes()
        {
            if (Package.TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            CheckDiskSize(Package.FileName, Package.TotalFileSize);
            if (Package.Options.OnTheFlyDownload == false) // store temp files on disk, so check disk size
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

        protected virtual void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            IsBusy = false;
            DownloadFileCompleted?.Invoke(this, e);
        }
        protected virtual void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            OnDownloadSpeedCalculator();
            DownloadProgressChanged?.Invoke(this, e);
        }
        protected virtual void OnChunkDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            ChunkDownloadProgressChanged?.Invoke(this, e);
        }
        protected virtual void OnDownloadSpeedCalculator()
        {
            // calc download speed
            var timeDiff = Environment.TickCount - LastDownloadCheckpoint + 1;
            if (timeDiff < 1000) return;
            var bytesDiff = Package.BytesReceived - BytesReceivedCheckPoint;
            DownloadSpeed = bytesDiff * 1000 / timeDiff;
            LastDownloadCheckpoint = Environment.TickCount;
            BytesReceivedCheckPoint = Package.BytesReceived;
        }
    }
}