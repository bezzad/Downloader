using System;

namespace Downloader.Test
{
    public class DownloadServiceEventsState
    {
        public bool DownloadStarted { get; set; }
        public string ActualFileName { get; set; }
        public bool DownloadSuccessfullCompleted { get; set; }
        public bool DownloadProgressIsCorrect { get; set; } = true;
        public int DownloadProgressCount { get; set; } = 0;
        public Exception DownloadError { get; set; }

        public DownloadServiceEventsState(IDownloadService downloadService)
        {
            downloadService.DownloadStarted += (s, e) => {
                DownloadStarted = true;
                ActualFileName = e.FileName;
            };

            downloadService.DownloadProgressChanged += (s, e) => {
                DownloadProgressCount++;
                DownloadProgressIsCorrect &= (e.ProgressPercentage == downloadService.Package.SaveProgress);
            };

            downloadService.DownloadFileCompleted += (s, e) => {
                DownloadSuccessfullCompleted = e.Error == null && !e.Cancelled;
                DownloadError = e.Error;
            };
        }
    }
}
