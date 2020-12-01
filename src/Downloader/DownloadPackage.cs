using System.Threading;

namespace Downloader
{
    public class DownloadPackage
    {
        // ReSharper disable once InconsistentNaming
        protected long _bytesReceived;
        public long BytesReceived
        {
            get => _bytesReceived;
            set => Interlocked.Exchange(ref _bytesReceived, value);
        }
        public long TotalFileSize { get; set; }
        public string FileName { get; set; }
        public Chunk[] Chunks { get; set; }
        public Request RequestInstance { get; set; }
        public DownloadConfiguration Options { get; set; }
    }
}
