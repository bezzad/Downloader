using System;
using System.Collections.Concurrent;
using System.ComponentModel;
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
            Timeout = 100000;
            BufferSize = 1024;
            Cts = new CancellationTokenSource();
        }


        protected long _bytesReceived;
        public EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        public EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        public int Timeout { get; set; }
        public bool IsBusy { get; set; }
        public string DownloadFileExtension { get; set; }
        public long BytesReceived => _bytesReceived;
        protected string DownloadFileName { get; set; }
        protected string FileName { get; set; }
        protected int BufferSize { get; set; }
        protected long TotalFileSize { get; set; }
        protected CancellationTokenSource Cts { get; set; }


        public void DownloadFileAsync(string address, string fileName, int parts = 0)
        {
            IsBusy = true;
            var uri = new Uri(address);

            // Handle number of parallel downloads  
            if (parts < 1)
                parts = Environment.ProcessorCount;

            Task.Run(async () =>
            {
                TotalFileSize = GetFileSize(uri);

                if (File.Exists(fileName))
                    File.Delete(fileName);

                using (var destinationStream = new FileStream(fileName, FileMode.Append))
                {
                    var tempFilesDictionary = new ConcurrentDictionary<long, byte[]>();
                    var ranges = ChunkFile(TotalFileSize, parts);

                    #region Parallel download  

                    // Parallel.ForEach(ranges, new ParallelOptions() { MaxDegreeOfParallelism = parts, CancellationToken = Cts.Token }, async range =>
                    foreach (var range in ranges)
                    {
                        try
                        {
                            if (WebRequest.Create(uri) is HttpWebRequest req)
                            {
                                req.Method = "GET";
                                req.Timeout = Timeout;
                                req.AddRange(range.Start, range.End);
                                var chunkSize = range.End - range.Start + 1;
                                var data = new byte[chunkSize];

                                using (var httpWebResponse = req.GetResponse() as HttpWebResponse)
                                {
                                    if (httpWebResponse == null)
                                        continue;

                                    tempFilesDictionary.TryAdd(range.Id, data);

                                    using (var stream = httpWebResponse.GetResponseStream())
                                    {
                                        if (stream == null)
                                            continue;

                                        var offset = 0;
                                        var remainBytesCount = chunkSize - offset;
                                        while (remainBytesCount > 0)
                                        {

                                            var readSize = await stream.ReadAsync(data, offset, remainBytesCount > BufferSize ? BufferSize : (int)remainBytesCount);
                                            Interlocked.Add(ref _bytesReceived, readSize);
                                            OnDownloadProgressChanged(new DownloadProgressChangedEventArgs(TotalFileSize, BytesReceived));
                                            offset += readSize;
                                            remainBytesCount = chunkSize - offset;
                                            Console.WriteLine("remainBytesCount: " + remainBytesCount);
                                        }
                                    }

                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            throw;
                        }
                    }//);

                    #endregion

                    #region Merge to single file  

                    // foreach (var range in ranges)
                    // {
                    //     var tempFileName = tempFilesDictionary[range.Id];
                    //     var tempFileBytes = File.ReadAllBytes(tempFileName);
                    //     destinationStream.Write(tempFileBytes, 0, tempFileBytes.Length);
                    //     File.Delete(tempFileName);
                    // }

                    #endregion
                }

            });
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
            chunks[parts - 1] = new Range(chunks.Any() ? chunks.Last().End + 1 : 0, fileSize - 1);
            return chunks;
        }

        protected virtual void OnDownloadFileCompleted(AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null && File.Exists(DownloadFileName))
            {
                if (File.Exists(FileName))
                    File.Delete(FileName);

                File.Move(DownloadFileName, FileName);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(DownloadFileName) && File.Exists(DownloadFileName))
                {
                    CancelAsync();
                    File.Delete(DownloadFileName);
                }
            }

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