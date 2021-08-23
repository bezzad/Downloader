using System;
using System.Collections.Generic;

namespace Downloader
{
    public interface IDownloadManager
    {
        int MaxNumberOfMultipleFileDownload { get; set; }
        int NumberOfDownloads { get; }
        event EventHandler<IDownloadRequest> AddNewDownload;
        event EventHandler<IDownloadRequest> DownloadStarted;
        event EventHandler<IDownloadRequest> DownloadCompleted;
        event EventHandler<IDownloadRequest> DownloadProgressChanged;

        List<IDownloadRequest> GetDownloadFiles();
        void DownloadAsync(params IDownloadRequest[] downloadInfos);        
        void CancelAsync(IDownloadRequest downloadInfo);
        void CancelAllAsync();
        void ClearAsync();
    }
}
