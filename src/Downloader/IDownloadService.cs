using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownloadService
    {
        bool IsBusy { get; }
        bool IsCancelled { get; }
        DownloadPackage Package { get; set; }
        public DownloadStatus Status { get; }

        event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        Task<Stream> DownloadFileTaskAsync(DownloadPackage package, CancellationToken cancellationToken = default);
        Task<Stream> DownloadFileTaskAsync(string address, CancellationToken cancellationToken = default);
        Task DownloadFileTaskAsync(string address, string fileName, CancellationToken cancellationToken = default);
        Task DownloadFileTaskAsync(string address, DirectoryInfo folder, CancellationToken cancellationToken = default);
        void CancelAsync();
        Task CancelTaskAsync();
        void Pause();
        void Resume();
        Task Clear();
    }
}