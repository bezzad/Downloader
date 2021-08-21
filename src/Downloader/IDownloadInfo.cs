namespace Downloader
{
    public interface IDownloadInfo
    {
        public bool IsSaving { get; set; }
        public bool IsSaveComplete { get; set; }
        public double SaveProgress { get; set; }
        public string Url { get; set; }
        public string Path { get; set; }
    }
}
