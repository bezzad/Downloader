using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public partial class DownloadService
    {
        public DownloadService()
        {
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.MaxServicePointIdleTime = 1000;
            DownloadFileExtension = ".download";
            Timeout = 5000;
            BufferBlockSize = 2048;
            MaxTryAgainOnFailover = 5;
            ParallelDownload = true;
            Cts = new CancellationTokenSource();
            DownloadedChunks = new ConcurrentDictionary<long, Chunk>();
        }


        /// <summary>
        /// Download of file chunks as Parallel
        /// </summary>
        public bool ParallelDownload { get; set; }
        public int Timeout { get; set; }
        public bool IsBusy { get; set; }
        public int ChunkCount { get; set; }
        public int BufferBlockSize { get; set; }
        public string DownloadFileExtension { get; }
        public long BytesReceived => _bytesReceived;
        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        // ReSharper disable once InconsistentNaming
        protected long _bytesReceived;
        protected string FileName { get; set; }
        protected long TotalFileSize { get; set; }
        protected ConcurrentDictionary<long, Chunk> DownloadedChunks { get; set; }
        protected CancellationTokenSource Cts { get; set; }
        protected int MaxTryAgainOnFailover { get; }



        public void DownloadFileAsync(string address, string fileName, int parts = 0)
        {
            FileName = fileName;
            IsBusy = true;
            var uri = new Uri(address);

            TotalFileSize = GetFileSize(uri);

            if (TotalFileSize <= 0)
                throw new InvalidDataException("File size is invalid!");

            var neededParts = (int)Math.Ceiling((double)TotalFileSize / int.MaxValue); // for files as larger than 2GB

            // Handle number of parallel downloads  
            ChunkCount = parts < neededParts ? neededParts : parts;

            Debug.WriteLine($"Total File Size: {TotalFileSize}");
            var chunks = ChunkFile(TotalFileSize, ChunkCount);
            ChunkCount = chunks.Length; // may be the parts length is less than defined count

            if (File.Exists(fileName))
                File.Delete(fileName);

            StartDownload(uri, chunks);
        }
        public void CancelAsync()
        {
            Cts?.Cancel(false);
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
                var range = new Chunk(chunk * chunkSize, Math.Min((chunk + 1) * chunkSize - 1, fileSize - 1));
                DownloadedChunks.TryAdd(range.Id, range);
            }

            return DownloadedChunks.Values.ToArray();
        }
        protected async void StartDownload(Uri address, Chunk[] chunks)
        {
            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                if (ParallelDownload)
                {   // download as parallel
                    var task = DownloadChunk(address, chunk, Cts.Token);
                    tasks.Add(task);
                }
                else
                {   // download as async and serial
                    await DownloadChunk(address, chunk, Cts.Token);
                }
            }

            if (ParallelDownload) // is parallel
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

                            var remainBytesCount = chunk.Length - chunk.Position;
                            while (remainBytesCount > 0)
                            {
                                if (token.IsCancellationRequested)
                                    return chunk;

                                using (var cts = new CancellationTokenSource(Timeout))
                                {
                                    var readSize = await stream.ReadAsync(chunk.Data, chunk.Position,
                                        remainBytesCount > BufferBlockSize ? BufferBlockSize : (int)remainBytesCount,
                                        cts.Token);
                                    Interlocked.Add(ref _bytesReceived, readSize);
                                    chunk.Position += readSize;
                                    remainBytesCount = chunk.Length - chunk.Position;

                                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(TotalFileSize, BytesReceived));
                                }
                            }
                        }

                        return chunk;
                    }
                }
            }
            catch (TaskCanceledException) // when stream reader timeout occured 
            {
                if (token.IsCancellationRequested == false)
                {
                    // re-request
                    await DownloadChunk(address, chunk, token);
                }
            }
            catch (WebException) // when the host forcibly closed the connection.
            {
                if (token.IsCancellationRequested == false &&
                    chunk.FailoverCount++ <= MaxTryAgainOnFailover)
                {
                    // re-request
                    await DownloadChunk(address, chunk, token);
                }
            }
            catch (Exception e) // Maybe no internet!
            {
                if (token.IsCancellationRequested == false &&
                    chunk.FailoverCount++ <= MaxTryAgainOnFailover &&
                    e.Source == "System.Net.Http" || e.Source == "System.Net.Socket")
                {
                    // wait and decrease speed to low pressure on host
                    Timeout += 1000;
                    await Task.Delay(Timeout, token);
                    // re-request
                    await DownloadChunk(address, chunk, token);
                }
                else
                    OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, null));
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

            using (var destinationStream = new FileStream(FileName, FileMode.Append))
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
            DownloadProgressChanged?.Invoke(this, e);
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
    }
}