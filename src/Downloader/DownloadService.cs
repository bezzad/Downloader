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
    public partial class DownloadService
    {
        public DownloadService(DownloadConfiguration options = null)
        {
            Package = new DownloadPackage()
            {
                Options = options ?? new DownloadConfiguration()
            };

            ServicePointManager.Expect100Continue = false; // accept the request for POST, PUT and PATCH verbs
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.MaxServicePointIdleTime = 1000;

            Cts = new CancellationTokenSource();
        }
        


        protected long BytesReceivedCheckPoint { get; set; }
        protected long LastDownloadCheckpoint { get; set; }
        protected CancellationTokenSource Cts { get; set; }
        /// <summary>
        /// Is in downloading time
        /// </summary>
        public bool IsBusy { get; protected set; }
        public long DownloadSpeed { get; set; }
        public DownloadPackage Package { get; set; }
        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;


        public async Task<DownloadPackage> DownloadFileAsync(DownloadPackage package)
        {
            IsBusy = true;
            Package = package;

            if (Package.TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            if (File.Exists(Package.FileName))
                File.Delete(Package.FileName);

            await StartDownload();

            return Package;
        }
        public async Task<DownloadPackage> DownloadFileAsync(string address, string fileName)
        {
            IsBusy = true;

            Package.FileName = fileName;
            Package.Address = new Uri(address);
            Package.TotalFileSize = GetFileSize(Package.Address);

            if (Package.TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            var neededParts = (int)Math.Ceiling((double)Package.TotalFileSize / int.MaxValue); // for files as larger than 2GB

            // Handle number of parallel downloads  
            var parts = Package.Options.ChunkCount < neededParts ? neededParts : Package.Options.ChunkCount;

            Package.Chunks = ChunkFile(Package.TotalFileSize, parts);

            if (File.Exists(Package.FileName))
                File.Delete(Package.FileName);

            await StartDownload();

            return Package;
        }
        public void CancelAsync()
        {
            Cts?.Cancel(false);
        }
        public void Clear()
        {
            Cts?.Dispose();
            Cts = new CancellationTokenSource();
            Package.FileName = null;
            Package.TotalFileSize = 0;
            Package.BytesReceived = 0;
            Package.Chunks = null;
        }

        protected long GetFileSize(Uri address)
        {
            var webRequest = WebRequest.Create(address);
            webRequest.Method = "HEAD";
            using (var webResponse = webRequest.GetResponse())
            {
                if (long.TryParse(webResponse.Headers.Get("Content-Length"), out var respLength))
                    return respLength;
            }

            return 0;
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
            var tasks = new List<Task>();
            foreach (var chunk in Package.Chunks)
            {
                if (Package.Options.ParallelDownload)
                {   // download as parallel
                    var task = DownloadChunk(Package.Address, chunk, Cts.Token);
                    tasks.Add(task);
                }
                else
                {   // download as async and serial
                    await DownloadChunk(Package.Address, chunk, Cts.Token);
                }
            }

            if (Package.Options.ParallelDownload) // is parallel
                Task.WaitAll(tasks.ToArray(), Cts.Token);
            //
            // Merge data to single file
            await MergeChunks(Package.Chunks);

            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, null));
        }
        protected async Task<Chunk> DownloadChunk(Uri address, Chunk chunk, CancellationToken token)
        {
            try
            {
                if (WebRequest.Create(address) is HttpWebRequest req)
                {
                    req.Method = "GET";
                    req.Timeout = int.MaxValue;
                    req.AddRange(chunk.Start + chunk.Position, chunk.End);

                    using (var httpWebResponse = req.GetResponse() as HttpWebResponse)
                    {
                        if (httpWebResponse == null)
                            return chunk;

                        var stream = httpWebResponse.GetResponseStream();
                        using (stream)
                        {
                            if (stream == null)
                                return chunk;

                            var bytesToReceiveCount = chunk.Length - chunk.Position;
                            while (bytesToReceiveCount > 0)
                            {
                                if (token.IsCancellationRequested)
                                    return chunk;

                                using (var cts = new CancellationTokenSource(Package.Options.Timeout))
                                {
                                    var readSize = await stream.ReadAsync(chunk.Data, chunk.Position,
                                        bytesToReceiveCount > Package.Options.BufferBlockSize
                                            ? Package.Options.BufferBlockSize
                                            : (int)bytesToReceiveCount,
                                        cts.Token);
                                    Package.BytesReceived += readSize;
                                    chunk.Position += readSize;
                                    bytesToReceiveCount = chunk.Length - chunk.Position;

                                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(
                                        Package.TotalFileSize, Package.BytesReceived, DownloadSpeed));
                                }
                            }
                        }

                        return chunk;
                    }
                }
            }
            catch (TaskCanceledException) when (token.IsCancellationRequested == false) // when stream reader timeout occured 
            {
                // re-request
                await DownloadChunk(address, chunk, token);
            }
            catch (WebException) when (token.IsCancellationRequested == false &&
                                      chunk.FailoverCount++ <= Package.Options.MaxTryAgainOnFailover) // when the host forcibly closed the connection.
            {
                await Task.Delay(Package.Options.Timeout, token);
                chunk.Checkpoint();
                // re-request
                await DownloadChunk(address, chunk, token);
            }
            catch (Exception e) when (token.IsCancellationRequested == false &&
                                     chunk.FailoverCount++ <= Package.Options.MaxTryAgainOnFailover &&
                                     (e.Source == "System.Net.Http" ||
                                      e.Source == "System.Net.Sockets" ||
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
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, null));
                Debugger.Break();
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

            using (var destinationStream = new FileStream(Package.FileName, FileMode.Append, FileAccess.Write))
            {
                foreach (var chunk in chunks.OrderBy(c => c.Start))
                {
                    await destinationStream.WriteAsync(chunk.Data, 0, (int)chunk.Length);
                }
            }
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