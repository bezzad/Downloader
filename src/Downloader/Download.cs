using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader
{
    internal class Download : IDownload
    {
        private readonly IDownloadService downloadService;

        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged
        {
            add { downloadService.ChunkDownloadProgressChanged += value; }
            remove { downloadService.ChunkDownloadProgressChanged -= value; }
        }

        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted
        {
            add { downloadService.DownloadFileCompleted += value; }
            remove { downloadService.DownloadFileCompleted -= value; }
        }

        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged
        {
            add { downloadService.DownloadProgressChanged += value; }
            remove { downloadService.DownloadProgressChanged -= value; }
        }

        public event EventHandler<DownloadStartedEventArgs> DownloadStarted
        {
            add { downloadService.DownloadStarted += value; }
            remove { downloadService.DownloadStarted -= value; }
        }

        public Download(
            string url,
            string path,
            DownloadConfiguration configuration)
        {
            downloadService =
                configuration is not null ?
                new DownloadService(configuration) :
                new DownloadService();
            Url=url;
            FilePath=path;
            Status = DownloadStatus.Created;
        }

        public Download(
            DownloadPackage package,
            DownloadConfiguration configuration)
        {
            downloadService = new DownloadService(configuration);
            Package = package;
            Status = DownloadStatus.Stopped;
        }

        public string Url { get; }
        public string FilePath { get; }
        public long DownloadedSize => downloadService.Package.ReceivedBytesSize;
        public DownloadPackage Package { get; private set; }

        public DownloadStatus Status { get; private set; }

        public async Task StartAsync()
        {
            if (Package is not null)
            {
                await downloadService.DownloadFileTaskAsync(Package);
                Package = downloadService.Package;
            }
            else
            {
                await downloadService.DownloadFileTaskAsync(Url, FilePath);
            }
            Status = DownloadStatus.Running;
        }

        public void Stop()
        {
            downloadService.CancelAsync();
            Status = DownloadStatus.Stopped;
        }

        public void Clear()
        {
            Stop();
            downloadService.Clear();
            Package = null;
            Status = DownloadStatus.Created;
        }
    }
}
