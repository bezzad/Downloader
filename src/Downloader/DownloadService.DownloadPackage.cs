namespace Downloader
{
    public partial class DownloadService
    {
        public class DownloadPackage
        {
            // ReSharper disable once InconsistentNaming
            protected long _bytesReceived;
            public long BytesReceived => _bytesReceived;
            public long TotalFileSize { get; set; }
            public string FileName { get; set; }
            public Chunk[] Chunks { get; set; }
        }
    }
}
