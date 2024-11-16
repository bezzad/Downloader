namespace Downloader.Test.Helper;

public class DownloadServiceEventsState
{
    public bool DownloadStarted { get; set; }
    public string ActualFileName { get; set; }
    public bool DownloadSuccessfulCompleted { get; set; }
    public bool IsDownloadCancelled { get; set; }
    public bool DownloadProgressIsCorrect { get; set; } = true;
    public int DownloadProgressCount { get; set; }
    public Exception DownloadError { get; set; }

    public DownloadServiceEventsState(IDownloadService downloadService)
    {
        downloadService.DownloadStarted += (_, e) => {
            DownloadStarted = true;
            ActualFileName = e.FileName;
        };

        downloadService.DownloadProgressChanged += (_, e) => {
            DownloadProgressCount++;
            DownloadProgressIsCorrect &= Math.Abs(e.ProgressPercentage - downloadService.Package.SaveProgress) < 0.1;
        };

        downloadService.DownloadFileCompleted += (_, e) => {
            DownloadSuccessfulCompleted = e.Error == null && !e.Cancelled;
            DownloadError = e.Error;
            IsDownloadCancelled = DownloadSuccessfulCompleted == false && DownloadError == null;
        };
    }
}
