using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    internal class Download : IDownload
    {
        private readonly IDownloadService downloadService;
        public string Url { get; }
        public string Folder { get; }
        public string Filename { get; }
        public long DownloadedFileSize => downloadService?.Package?.ReceivedBytesSize ?? 0;
        public long TotalFileSize => downloadService?.Package?.TotalFileSize ?? DownloadedFileSize;
        public DownloadPackage Package { get; private set; }
        public DownloadStatus Status { get; private set; }

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
            string filename,
            DownloadConfiguration configuration)
        {
            downloadService =
                configuration is not null ?
                new DownloadService(configuration) :
                new DownloadService();

            Url = url;
            Folder = path;
            Filename = filename;
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

        public async Task StartAsync()
        {
            if (Package is not null)
            {
                await downloadService.DownloadFileTaskAsync(Package);
                Package = downloadService.Package;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Filename))
                {
                    await downloadService.DownloadFileTaskAsync(Url, new DirectoryInfo(Folder));
                }
                else
                {
                    await downloadService.DownloadFileTaskAsync(Url, Path.Combine(Folder, Filename));
                }
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

        public override bool Equals(object obj)
        {
            return obj is Download download &&
                   GetHashCode() == download.GetHashCode();
        }

        public override int GetHashCode()
        {
            int hashCode = 37;
            hashCode = (hashCode * 7) + Url.GetHashCode();
            hashCode = (hashCode * 7) + DownloadedFileSize.GetHashCode();
            return hashCode;
        }
    }
}
