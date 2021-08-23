using System;
using System.Collections.Generic;

namespace Downloader
{
    public interface IDownloadManager
    {
        /// <summary>
        /// Maximum degree of parallelism of downloads
        /// </summary>
        int MaxConcurrentDownloadsDegree { get; set; }

        /// <summary>
        /// The count of ongoing downloads
        /// </summary>
        int NumberOfDownloadsInProgress { get; }

        event EventHandler<IDownloadRequest> AddNewDownload;
        event EventHandler<IDownloadRequest> DownloadStarted;
        event EventHandler<IDownloadRequest> DownloadCompleted;
        event EventHandler<IDownloadRequest> DownloadProgressChanged;

        IEnumerable<IDownloadRequest> GetDownloadRequests();

        void DownloadAsync(string url, string path);
        void DownloadAsync(params IDownloadRequest[] downloadRequests);    
        
        void CancelAsync(string url);
        void CancelAsync(IDownloadRequest downloadRequest);
        void CancelAllAsync();

        void ClearAsync();
    }
}
