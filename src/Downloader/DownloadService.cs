using System;
using System.Collections.Concurrent;
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
            Package = new DownloadPackage();
            Options = options ?? new DownloadConfiguration();

            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.MaxServicePointIdleTime = 1000;

            Cts = new CancellationTokenSource();
            DownloadedChunks = new ConcurrentDictionary<long, Chunk>();
        }


        // ReSharper disable once InconsistentNaming
        protected long _bytesReceived;
        protected long BytesReceivedCheckPoint { get; set; }
        protected long LastDownloadCheckpoint { get; set; }
        protected ConcurrentDictionary<long, Chunk> DownloadedChunks { get; set; }
        protected CancellationTokenSource Cts { get; set; }
        /// <summary>
        /// Is in downloading time
        /// </summary>
        public bool IsBusy { get; protected set; }
        public string FileName { get; set; }
        public long BytesReceived => _bytesReceived;
        public long TotalFileSize { get; set; }
        public long DownloadSpeed { get; set; }
        public DownloadPackage Package { get; set; }
        public DownloadConfiguration Options { get; set; }
        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;



        public async Task DownloadFileAsync(string address, string fileName)
        {
            FileName = fileName;
            IsBusy = true;
            var uri = new Uri(address);
            TotalFileSize = GetFileSize(uri);

            if (TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            var neededParts = (int)Math.Ceiling((double)TotalFileSize / int.MaxValue); // for files as larger than 2GB

            // Handle number of parallel downloads  
            var parts = Options.ChunkCount < neededParts ? neededParts : Options.ChunkCount;

            var chunks = ChunkFile(TotalFileSize, parts);

            if (File.Exists(fileName))
                File.Delete(fileName);

            await StartDownload(uri, chunks);
        }
        public void CancelAsync()
        {
            Cts?.Cancel(false);
        }
        public void Clear()
        {
            Cts?.Dispose();
            Cts = new CancellationTokenSource();
            FileName = null;
            TotalFileSize = 0;
            _bytesReceived = 0;
            DownloadedChunks.Clear();
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

            for (var chunk = 0; chunk < parts; chunk++)
            {
                var range =
                    (chunk == parts - 1) 
                        ? new Chunk(chunk * chunkSize, fileSize - 1) // last chunk
                        : new Chunk(chunk * chunkSize, (chunk + 1) * chunkSize - 1);

                DownloadedChunks.TryAdd(range.Id, range);
            }

            return DownloadedChunks.Values.ToArray();
        }
        protected async Task StartDownload(Uri address, Chunk[] chunks)
        {
            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                if (Options.ParallelDownload)
                {   // download as parallel
                    var task = DownloadChunk(address, chunk, Cts.Token);
                    tasks.Add(task);
                }
                else
                {   // download as async and serial
                    await DownloadChunk(address, chunk, Cts.Token);
                }
            }

            if (Options.ParallelDownload) // is parallel
                Task.WaitAll(tasks.ToArray(), Cts.Token);
            //
            // Merge data to single file
            await MergeChunks(chunks);

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

                                using (var cts = new CancellationTokenSource(Options.Timeout))
                                {
                                    var readSize = await stream.ReadAsync(chunk.Data, chunk.Position,
                                        bytesToReceiveCount > Options.BufferBlockSize
                                            ? Options.BufferBlockSize
                                            : (int)bytesToReceiveCount,
                                        cts.Token);
                                    Interlocked.Add(ref _bytesReceived, readSize);
                                    chunk.Position += readSize;
                                    bytesToReceiveCount = chunk.Length - chunk.Position;

                                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(
                                        TotalFileSize, BytesReceived, DownloadSpeed));
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
                                      chunk.FailoverCount++ <= Options.MaxTryAgainOnFailover) // when the host forcibly closed the connection.
            {
                await Task.Delay(Options.Timeout, token);
                chunk.Checkpoint();
                // re-request
                await DownloadChunk(address, chunk, token);
            }
            catch (Exception e) when (token.IsCancellationRequested == false &&
                                     chunk.FailoverCount++ <= Options.MaxTryAgainOnFailover &&
                                     (e.Source == "System.Net.Http" ||
                                      e.Source == "System.Net.Sockets" ||
                                      e.InnerException is SocketException))
            {
                // wait and decrease speed to low pressure on host
                Options.Timeout += chunk.CanContinue() ? 0 : 500;
                chunk.Checkpoint();
                await Task.Delay(Options.Timeout, token);
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
            var directory = Path.GetDirectoryName(FileName);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            using (var destinationStream = new FileStream(FileName, FileMode.Append, FileAccess.Write))
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
            var bytesDiff = BytesReceived - BytesReceivedCheckPoint;
            DownloadSpeed = bytesDiff * 1000 / timeDiff;
            LastDownloadCheckpoint = Environment.TickCount;
            BytesReceivedCheckPoint = BytesReceived;
        }
    }
}