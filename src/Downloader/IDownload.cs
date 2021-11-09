using System.ComponentModel;
using System;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownload
    {
        string Url { get; }
        string Folder { get; }
        string Filename { get; }
        long DownloadedFileSize { get; }
        long TotalFileSize { get; }
        DownloadStatus Status { get; }

        event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        void Clear();
        Task StartAsync();
        void Stop();
    }
}
