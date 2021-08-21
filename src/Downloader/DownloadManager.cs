using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Downloader
{
    public class DownloadManager : IDownloadManager
    {
        private Dictionary<string, DownloadRequest> _requests;
        private int _numberOfDownloads;
        public int NumberOfDownloads => _numberOfDownloads;
        public int MaxNumberOfMultipleFileDownload { get; set; }
        public DownloadConfiguration Configuration { get; }
        public event EventHandler<IDownloadInfo> AddNewDownload;
        public event EventHandler<IDownloadInfo> DownloadStarted;
        public event EventHandler<IDownloadInfo> DownloadCompleted;
        public event EventHandler<IDownloadInfo> DownloadProgressChanged;

        public DownloadManager(DownloadConfiguration configuration, int maxNumberOfMultipleFileDownload)
        {
            Configuration = configuration;
            MaxNumberOfMultipleFileDownload = maxNumberOfMultipleFileDownload;
        }

        public void DownloadAsync(params IDownloadInfo[] downloadInfos)
        {
            foreach (var download in downloadInfos)
            {
                DownloadAsync(download);
            }
        }
        private void DownloadAsync(IDownloadInfo downloadInfo)
        {
            if (string.IsNullOrWhiteSpace(downloadInfo?.Url))
                throw new ArgumentNullException(nameof(downloadInfo.Url));

            var request = GetOrAddDownloadService(downloadInfo);
        }

        private DownloadRequest GetOrAddDownloadService(IDownloadInfo downloadInfo)
        {
            if (_requests.TryGetValue(downloadInfo.Url, out var request))
                return request;

            return CreateDownloadRequest(downloadInfo);
        }
        private DownloadRequest CreateDownloadRequest(IDownloadInfo downloadInfo)
        {
            var downloader = new DownloadService(Configuration);
            downloader.DownloadFileCompleted += OnDownloadFileComplete;
            var request = new DownloadRequest(downloadInfo, downloader);
            _requests.Add(downloadInfo.Url, request);
            OnAddNewDownload(downloadInfo);
            return request;
        }
        private void RemoveRequest(DownloadRequest request)
        {
            lock (this)
            {
                Interlocked.Decrement(ref _numberOfDownloads);
                Debug.Assert(_numberOfDownloads >= 0, "This algorithm isn't thread-safe! What do you do?");
                request.DownloadInfo.IsSaving = false;
            }
        }

        private void OnDownloadFileComplete(object sender, AsyncCompletedEventArgs e)
        {
            if (sender is DownloadService downloader &&
               _requests.TryGetValue(downloader.Package.Address, out var request))
            {
                RemoveRequest(request);
                if (e.Cancelled == false)
                {
                    if (e.Error == null) // download completed
                    {
                        //onDownloadCompleted?.Invoke(request.DownloadInfo);
                    }
                    else
                    {
                        //_crashHandler.HandleException(e.Error);
                    }
                }
                EnqueueDownload();
            }
        }
        private void EnqueueDownload()
        {
            lock (this)
            {
                if (_numberOfDownloads < MaxNumberOfMultipleFileDownload)
                {
                    var request = EnqueueRequest();
                    if (request != null)
                    {
                        Interlocked.Increment(ref _numberOfDownloads);
                        Debug.Assert(_numberOfDownloads <= MaxNumberOfMultipleFileDownload, "This algorithm isn't thread-safe! What do you do?");
                        request.Downloader.DownloadFileTaskAsync(request.DownloadInfo.Url, request.DownloadInfo.Path).ConfigureAwait(false);
                    }
                }
            }
        }

        private DownloadRequest EnqueueRequest()
        {
            return _requests.Values.FirstOrDefault(req =>
                req.IsBusy == false && req.DownloadInfo.IsSaving &&
                req.DownloadInfo.IsSaveComplete == false);
        }

        private void OnAddNewDownload(IDownloadInfo request)
        {
            AddNewDownload?.Invoke(this, request);
        }

        public List<IDownloadInfo> GetDownloadFiles()
        {
            throw new NotImplementedException();
        }

        public void CancelAsync(IDownloadInfo downloadInfo)
        {
            throw new NotImplementedException();
        }

        public void CancelAllAsync()
        {
            throw new NotImplementedException();
        }

        public void ClearAsync()
        {
            throw new NotImplementedException();
        }
    }
}
