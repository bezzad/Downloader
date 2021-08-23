namespace Downloader
{
    public interface IDownloadRequest
    {
        public bool IsSaving { get; set; }
        public bool IsSaveComplete { get; set; }
        public double SaveProgress { get; set; }
        public string Url { get; set; }
        public string Path { get; set; }
        public IDownloadService DownloadService { get; set; }
    }
}
