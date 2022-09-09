using System;
using System.ComponentModel;
using System.IO;
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

        Task<Stream> DownloadFileTaskAsync(DownloadPackage package);
        Task<Stream> DownloadFileTaskAsync(string address);
        Task DownloadFileTaskAsync(string address, string fileName);
        Task DownloadFileTaskAsync(string address, DirectoryInfo folder);
        void CancelAsync();
        void Pause();
        void Resume();
        void Clear();
    }
}