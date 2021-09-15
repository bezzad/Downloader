﻿using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Downloader
{
    internal class Download : IDownload
    {
        private readonly IDownloadService downloadService;

        public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged
        {
            add { downloadService.ChunkDownloadProgressChanged += value; }
            remove { downloadService.ChunkDownloadProgressChanged -= value; }
        }

        public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted
        {
            add { downloadService.DownloadFileCompleted += value; }
            remove { downloadService.DownloadFileCompleted -= value; }
        }

        public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged
        {
            add { downloadService.DownloadProgressChanged += value; }
            remove { downloadService.DownloadProgressChanged -= value; }
        }

        public event EventHandler<DownloadStartedEventArgs> DownloadStarted
        {
            add { downloadService.DownloadStarted += value; }
            remove { downloadService.DownloadStarted -= value; }
        }

        public Download(
            string url,
            string fullPath,
            DownloadConfiguration configuration)
        {
            downloadService = new DownloadService(configuration);
            Url=url;
            FileFullPath=fullPath;
        }

        public Download(
            DownloadPackage package,
            DownloadConfiguration configuration)
        {
            downloadService = new DownloadService(configuration);
            Package = package;
        }

        public string Url { get; }
        public string FileFullPath { get; }
        public long DownloadedSize => downloadService.Package.ReceivedBytesSize;
        public DownloadPackage Package { get; private set; }

        public async Task StartAsync()
        {
            if (Package is not null)
            {
                await downloadService.DownloadFileTaskAsync(Package);
                Package = downloadService.Package;
            }
            else
            {
                await downloadService.DownloadFileTaskAsync(Url, FileFullPath);
            }
        }

        public void Stop()
        {
            downloadService.CancelAsync();
        }

        public void Reset()
        {
            Stop();
            downloadService.Clear();
            Package = null;
        }
    }
}
