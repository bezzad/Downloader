using System;
using System.IO;
using System.Threading;

namespace Downloader
{
    public class DownloadPackage
    {
        private long _receivedBytesSize;
        public long ReceivedBytesSize
        {
            get => _receivedBytesSize;
            set => Interlocked.Exchange(ref _receivedBytesSize, value);
        }

        public long TotalFileSize { get; set; }
        public string FileName { get; set; }
        public Chunk[] Chunks { get; set; }
        public Uri Address { get; set; }
        public DownloadConfiguration Options { get; set; }
        internal Stream DestinationStream { get; set; }

        public void AddReceivedBytes(long size)
        {
            Interlocked.Add(ref _receivedBytesSize, size);
        }
    }
}