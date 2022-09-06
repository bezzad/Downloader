namespace Downloader
{
    public enum DownloadStatus
    {
        None = 0,
        Created = 1,
        Running = 2,
        Stopped = 3, // Cancelled
        Paused = 4,
        Completed = 5,
        Failed = 6
    }
}
