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
        public DownloadStatus Status => Package?.Status ?? DownloadStatus.None;

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

        public Download(string url, string path, string filename, DownloadConfiguration configuration)
        {
            downloadService = new DownloadService(configuration);
            Url = url;
            Folder = path;
            Filename = filename;
            Package = downloadService.Package;
        }

        public Download(DownloadPackage package, DownloadConfiguration configuration)
        {
            downloadService = new DownloadService(configuration);
            Package = package;
        }

        public async Task<Stream> StartAsync()
        {
            if (string.IsNullOrWhiteSpace(Package?.Address))
            {
                if (string.IsNullOrWhiteSpace(Folder) && string.IsNullOrWhiteSpace(Filename))
                {
                    return await downloadService.DownloadFileTaskAsync(Url);
                }
                else if (string.IsNullOrWhiteSpace(Filename))
                {
                    await downloadService.DownloadFileTaskAsync(Url, new DirectoryInfo(Folder));
                    return null;
                }
                else
                {
                    // with Folder and Filename
                    await downloadService.DownloadFileTaskAsync(Url, Path.Combine(Folder, Filename));
                    return null;
                }
            }
            else
            {
                return await downloadService.DownloadFileTaskAsync(Package);
            }
        }

        public void Stop()
        {
            downloadService.CancelAsync();
        }

        public void Pause()
        {
            downloadService.Pause();
        }

        public void Resume()
        {
            downloadService.Resume();
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

        public async void Dispose()
        {
            await downloadService.Clear();
            Package = null;
        }
    }
}
