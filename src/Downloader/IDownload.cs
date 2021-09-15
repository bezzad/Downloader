using System.ComponentModel;
using System;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownload
    {
        string Url { get; }
        string FileFullPath { get; }
        long DownloadedSize { get; }

        event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        void Reset();
        Task StartAsync();
        void Stop();
    }
}
