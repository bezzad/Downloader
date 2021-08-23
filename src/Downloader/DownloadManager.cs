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
        private Dictionary<string, IDownloadRequest> _requests;
        private int _numberOfDownloads;
        public int NumberOfDownloads => _numberOfDownloads;
        public int MaxNumberOfMultipleFileDownload { get; set; }
        public DownloadConfiguration Configuration { get; }
        public event EventHandler<IDownloadRequest> AddNewDownload;
        public event EventHandler<IDownloadRequest> DownloadStarted;
        public event EventHandler<IDownloadRequest> DownloadCompleted;
        public event EventHandler<IDownloadRequest> DownloadProgressChanged;

        public DownloadManager(DownloadConfiguration configuration, int maxNumberOfMultipleFileDownload)
        {
            if(maxNumberOfMultipleFileDownload <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumberOfMultipleFileDownload), maxNumberOfMultipleFileDownload, "The value must be greater than zero.");
            }

            Configuration = configuration;
            MaxNumberOfMultipleFileDownload = maxNumberOfMultipleFileDownload;
        }

        public void DownloadAsync(params IDownloadRequest[] downloadInfos)
        {
            foreach (var download in downloadInfos)
            {
                DownloadAsync(download);
            }
        }
        private void DownloadAsync(IDownloadRequest downloadInfo)
        {
            if (string.IsNullOrWhiteSpace(downloadInfo?.Url))
                throw new ArgumentNullException(nameof(downloadInfo.Url));

            var request = GetOrAddDownloadService(downloadInfo);
        }

        private IDownloadRequest GetOrAddDownloadService(IDownloadRequest downloadInfo)
        {
            if (_requests.TryGetValue(downloadInfo.Url, out var request))
                return request;

            return CreateDownloadRequest(downloadInfo);
        }
        private IDownloadRequest CreateDownloadRequest(IDownloadRequest downloadInfo)
        {
            //var downloader = new DownloadService(Configuration);
            //downloader.DownloadFileCompleted += OnDownloadFileComplete;
            //var request = new DownloadRequest(downloadInfo, downloader);
            //_requests.Add(downloadInfo.Url, request);
            //OnAddNewDownload(downloadInfo);
            return downloadInfo;
        }
        private void RemoveRequest(IDownloadRequest request)
        {
            lock (this)
            {
                Interlocked.Decrement(ref _numberOfDownloads);
                Debug.Assert(_numberOfDownloads >= 0, "This algorithm isn't thread-safe! What do you do?");
                request.IsSaving = false;
            }
        }

        private void OnDownloadFileComplete(object sender, AsyncCompletedEventArgs e)
        {
            if (sender is IDownloadService downloader &&
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
                        request.DownloadService.DownloadFileTaskAsync(request.Url, request.Path).ConfigureAwait(false);
                    }
                }
            }
        }

        private IDownloadRequest EnqueueRequest()
        {
            return _requests.Values.FirstOrDefault(req =>
                req.IsSaving && req.IsSaveComplete == false);
        }

        private void OnAddNewDownload(IDownloadRequest request)
        {
            AddNewDownload?.Invoke(this, request);
        }

        public List<IDownloadRequest> GetDownloadFiles()
        {
            throw new NotImplementedException();
        }

        public void CancelAsync(IDownloadRequest downloadInfo)
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
