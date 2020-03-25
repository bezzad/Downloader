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
    public partial class DownloadService : IDisposable
    {
        public DownloadService()
        {
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 100;
            ServicePointManager.MaxServicePointIdleTime = 1000;
            DownloadFileExtension = ".download";
            Timeout = 5000;
            BufferBlockSize = 2048;
            Cts = new CancellationTokenSource();
            DownloadedChunks = new ConcurrentDictionary<long, Chunk>();
        }



        /// <summary>
        /// Download of file chunks as Parallel
        /// </summary>
        public bool IsMultipart { get; set; }
        public int Timeout { get; set; }
        public bool IsBusy { get; set; }
        public int ChunkCount { get; set; }
        public int BufferBlockSize { get; set; }
        public string DownloadFileExtension { get; set; }
        public long BytesReceived => _bytesReceived;
        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

        // ReSharper disable once InconsistentNaming
        protected long _bytesReceived;
        protected string DownloadFileName { get; set; }
        protected string FileName { get; set; }
        protected long TotalFileSize { get; set; }
        protected ConcurrentDictionary<long, Chunk> DownloadedChunks { get; set; }
        protected CancellationTokenSource Cts { get; set; }



        public void DownloadFileAsync(string address, string fileName, int parts = 0)
        {
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

            if (File.Exists(fileName))
                File.Delete(fileName);

            StartDownload(uri, fileName, chunks);
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
            var chunkSize = fileSize / parts;
            for (var chunk = 0; chunk < parts; chunk++)
            {
                var range = new Chunk(chunk * chunkSize, Math.Min((chunk + 1) * chunkSize - 1, fileSize - 1));
                DownloadedChunks.TryAdd(range.Id, range);
            }

            return DownloadedChunks.Values.ToArray();
        }
        protected async void StartDownload(Uri address, string fileName, Chunk[] chunks)
        {
            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                if (IsMultipart)
                {   // download as parallel
                    var task = DownloadChunk(address, chunk, Cts.Token);
                    tasks.Add(task);
                }
                else
                {   // download as async and serial
                    await DownloadChunk(address, chunk, Cts.Token);
                }
            }

            if (IsMultipart) // is parallel
                Task.WaitAll(tasks.ToArray(), Cts.Token);
            //
            // Merge data to single file
            await MergeChunks(fileName, chunks);

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
                    req.AddRange(chunk.Start, chunk.End);

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
                                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(TotalFileSize, BytesReceived));
                                    chunk.Position += readSize;
                                    remainBytesCount = chunk.Length - chunk.Position;
                                }
                            }
                        }

                        return chunk;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                if (token.IsCancellationRequested == false)
                {
                    // re-request
                    await DownloadChunk(address, chunk, token);
                }
            }
            catch (Exception e)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, null));
            }

            return chunk;
        }
        protected async Task MergeChunks(string fileName, Chunk[] chunks)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            using (var destinationStream = new FileStream(fileName, FileMode.Append))
            {
                foreach (var chunk in chunks.OrderBy(c => c.Start))
                {
                    await destinationStream.WriteAsync(chunk.Data, 0, (int)chunk.Length);
                }
            }
        }


        protected virtual void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            // if (!e.Cancelled && e.Error == null && File.Exists(DownloadFileName))
            // {
            //     if (File.Exists(FileName))
            //         File.Delete(FileName);
            //
            //     File.Move(DownloadFileName, FileName);
            // }
            // else
            // {
            //     if (!string.IsNullOrWhiteSpace(DownloadFileName) && File.Exists(DownloadFileName))
            //     {
            //         CancelAsync();
            //         File.Delete(DownloadFileName);
            //     }
            // }

            IsBusy = false;
            DownloadFileCompleted?.Invoke(this, e);
        }
        protected virtual void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            DownloadProgressChanged?.Invoke(this, e);
        }
        
        public void Dispose()
        {
            Cts?.Dispose();
            DownloadedChunks.Clear();

            GC.SuppressFinalize(this);
        }
    }
}