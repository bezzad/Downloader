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
    public class DownloadService
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
        }



        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public int Timeout { get; set; }
        public bool IsBusy { get; set; }
        public int ChunkCount { get; set; }
        public int BufferBlockSize { get; set; }
        public string DownloadFileExtension { get; set; }
        public long BytesReceived => _bytesReceived;

        // ReSharper disable once InconsistentNaming
        protected long _bytesReceived;
        protected string DownloadFileName { get; set; }
        protected string FileName { get; set; }
        protected long TotalFileSize { get; set; }
        protected ConcurrentDictionary<long, byte[]> DownloadedChunks { get; set; }
        protected CancellationTokenSource Cts { get; set; }


        public void DownloadFileAsync(string address, string fileName, int parts = 0)
        {
            IsBusy = true;
            var uri = new Uri(address);

            // Handle number of parallel downloads  
            ChunkCount = parts < 1 ? Environment.ProcessorCount : parts;

            TotalFileSize = GetFileSize(uri);
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
        protected Range[] ChunkFile(long fileSize, int parts)
        {
            var chunks = new Range[parts];
            var chunkSize = fileSize / parts;
            for (var chunk = 0; chunk < parts - 1; chunk++)
            {
                chunks[chunk] = new Range(chunk * chunkSize, (chunk + 1) * chunkSize - 1);
            }
            chunks[parts - 1] = new Range(parts > 1 ? chunks[parts - 2].End + 1 : 0, fileSize - 1);
            return chunks;
        }
        protected async void StartDownload(Uri address, string fileName, Range[] chunks)
        {
            async Task DownloadJob(Range chunk)
            {
                var chunkData = await DownloadChunk(address, chunk, Cts.Token);
                if (chunkData != null)
                    DownloadedChunks.TryAdd(chunk.Id, chunkData);
            }
            DownloadedChunks = new ConcurrentDictionary<long, byte[]>();

            var tasks = new List<Task>();
            foreach (var chunk in chunks)
            {
                var task = DownloadJob(chunk);
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray(), Cts.Token);

            //
            // Merge data to single file
            await MergeChunks(fileName, chunks);

            OnDownloadFileCompleted(new AsyncCompletedEventArgs(null, false, null));
        }
        protected async Task<byte[]> DownloadChunk(Uri address, Range chunk, CancellationToken token)
        {
            var chunkSize = chunk.End - chunk.Start + 1;
            var data = new byte[chunkSize];
            var offset = 0;

            try
            {
                if (WebRequest.Create(address) is HttpWebRequest req)
                {
                    req.Method = "GET";
                    req.AddRange(chunk.Start, chunk.End);

                    using (var httpWebResponse = req.GetResponse() as HttpWebResponse)
                    {
                        if (httpWebResponse == null)
                            return data.Take(offset).ToArray();

                        var stream = httpWebResponse.GetResponseStream();
                        using (stream)
                        {
                            if (stream == null)
                                return data.Take(offset).ToArray();

                            var remainBytesCount = chunkSize - offset;
                            while (remainBytesCount > 0)
                            {
                                if (token.IsCancellationRequested)
                                    return data.Take(offset).ToArray();

                                using (var cts = new CancellationTokenSource(Timeout))
                                {
                                    var readSize = await stream.ReadAsync(data, offset,
                                        remainBytesCount > BufferBlockSize ? BufferBlockSize : (int)remainBytesCount,
                                        cts.Token);
                                    Interlocked.Add(ref _bytesReceived, readSize);
                                    OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(TotalFileSize, BytesReceived));
                                    offset += readSize;
                                    remainBytesCount = chunkSize - offset;
                                }
                            }
                        }

                        return data;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                if (token.IsCancellationRequested == false)
                {
                    // re-request
                    var continuedData = await DownloadChunk(address, new Range(chunk.Start + offset, chunk.End), token);
                    if (continuedData == null)
                    {
                        Debugger.Break(); // why???
                    }
                    else
                    {
                        var fromIndex = offset;
                        foreach (var b in continuedData)
                            data[fromIndex++] = b;

                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                OnDownloadFileCompleted(new AsyncCompletedEventArgs(e, false, null));
            }

            return data.Take(offset).ToArray();
        }
        protected async Task MergeChunks(string fileName, Range[] chunks)
        {
            var directory = Path.GetDirectoryName(fileName);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            if (Directory.Exists(directory) == false)
                Directory.CreateDirectory(directory);

            using (var destinationStream = new FileStream(fileName, FileMode.Append))
            {
                foreach (var chunk in chunks)
                {
                    if (DownloadedChunks.TryGetValue(chunk.Id, out var data))
                    {
                        await destinationStream.WriteAsync(data, 0, data.Length);
                    }
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


        protected struct Range
        {
            public Range(long start, long end)
            {
                Start = start;
                End = end;
            }

            public long Id => Start.PairingFunction(End);
            public long Start { get; set; }
            public long End { get; set; }
        }
    }
}