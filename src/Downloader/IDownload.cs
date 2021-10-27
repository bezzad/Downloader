using System.ComponentModel;
using System;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownload
    {
        string Url { get; }
        string FilePath { get; }
        long DownloadedSize { get; }
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
