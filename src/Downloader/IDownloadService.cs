using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace Downloader
{
    public interface IDownloadService
    {
        bool IsBusy { get; }
        long DownloadSpeed { get; }
        DownloadPackage Package { get; set; }
        event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;
        event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;
        event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;
        event EventHandler<DownloadStartedEventArgs> DownloadStarted;

        Task DownloadFileAsync(DownloadPackage package);
        Task DownloadFileAsync(string address, string fileName);
        Task DownloadFileAsync(string address, DirectoryInfo folder);
        void CancelAsync();
        void Clear();
    }
}