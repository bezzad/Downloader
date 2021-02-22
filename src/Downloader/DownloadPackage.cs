using System;
using System.Threading;

namespace Downloader
{
    [Serializable]
    public class DownloadPackage
    {
        private long _receivedBytesSize;
        public long ReceivedBytesSize
        {
            get => _receivedBytesSize;
            set => Interlocked.Exchange(ref _receivedBytesSize, value);
        }
        public string Address { get; set; }
        public long TotalFileSize { get; set; }
        public string FileName { get; set; }
        public Chunk[] Chunks { get; set; }

        public void AddReceivedBytes(long size)
        {
            Interlocked.Add(ref _receivedBytesSize, size);
        }
    }
}