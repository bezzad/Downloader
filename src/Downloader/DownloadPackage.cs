using System;
using System.Threading;

namespace Downloader
{
    public class DownloadPackage
    {
        private long _bytesReceived;

        public long BytesReceived
        {
            get => _bytesReceived;
            set => Interlocked.Exchange(ref _bytesReceived, value);
        }

        public long TotalFileSize { get; set; }
        public string FileName { get; set; }
        public Chunk[] Chunks { get; set; }
        public Uri Address { get; set; }
        public DownloadConfiguration Options { get; set; }
    }
}