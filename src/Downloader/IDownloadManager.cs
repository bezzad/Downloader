using System;
using System.Collections.Generic;

namespace Downloader
{
    public interface IDownloadManager
    {
        int MaxNumberOfMultipleFileDownload { get; set; }
        int NumberOfDownloads { get; }
        event EventHandler<IDownloadInfo> AddNewDownload;
        event EventHandler<IDownloadInfo> DownloadStarted;
        event EventHandler<IDownloadInfo> DownloadCompleted;
        event EventHandler<IDownloadInfo> DownloadProgressChanged;

        List<IDownloadInfo> GetDownloadFiles();
        void DownloadAsync(params IDownloadInfo[] downloadInfos);        
        void CancelAsync(IDownloadInfo downloadInfo);
        void CancelAllAsync();
        void ClearAsync();
    }
}
