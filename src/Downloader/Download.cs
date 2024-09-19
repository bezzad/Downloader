using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

internal class Download : IDownload
{
    private readonly IDownloadService _downloadService;
    public string Url { get; }
    public string Folder { get; }
    public string Filename { get; }
    public long DownloadedFileSize => _downloadService?.Package?.ReceivedBytesSize ?? 0;
    public long TotalFileSize => _downloadService?.Package?.TotalFileSize ?? DownloadedFileSize;
    public DownloadPackage Package { get; private set; }
    public DownloadStatus Status => Package?.Status ?? DownloadStatus.None;

    public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged
    {
        add => _downloadService.ChunkDownloadProgressChanged += value;
        remove => _downloadService.ChunkDownloadProgressChanged -= value;
    }

    public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted
    {
        add => _downloadService.DownloadFileCompleted += value;
        remove => _downloadService.DownloadFileCompleted -= value;
    }

    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged
    {
        add => _downloadService.DownloadProgressChanged += value;
        remove => _downloadService.DownloadProgressChanged -= value;
    }

    public event EventHandler<DownloadStartedEventArgs> DownloadStarted
    {
        add => _downloadService.DownloadStarted += value;
        remove => _downloadService.DownloadStarted -= value;
    }

    public Download(string url, string path, string filename, DownloadConfiguration configuration)
    {
        _downloadService = new DownloadService(configuration);
        Url = url;
        Folder = path;
        Filename = filename;
        Package = _downloadService.Package;
    }

    public Download(DownloadPackage package, DownloadConfiguration configuration)
    {
        _downloadService = new DownloadService(configuration);
        Package = package;
    }

    public Download(DownloadPackage package, string address, DownloadConfiguration configuration)
    {
        _downloadService = new DownloadService(configuration);
        Package = package;
        Url = address;
    }

    public async Task<Stream> StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(Package?.Urls?.FirstOrDefault()))
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                if (string.IsNullOrWhiteSpace(Folder))
                {
                    // store on memory stream so return stream
                    return await _downloadService.DownloadFileTaskAsync(Url, cancellationToken).ConfigureAwait(false);
                }

                // store on a file with the given path and url fetching name
                await _downloadService.DownloadFileTaskAsync(Url,
                    new DirectoryInfo(Folder), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // // store on a file with the given name and folder path
                await _downloadService.DownloadFileTaskAsync(Url, Path.Combine(Folder, Filename), cancellationToken)
                    .ConfigureAwait(false);                
            }
            
            return Stream.Null;
        }

        if (string.IsNullOrWhiteSpace(Url))
        {
            return await _downloadService.DownloadFileTaskAsync(Package, cancellationToken).ConfigureAwait(false);
        }

        return await _downloadService.DownloadFileTaskAsync(Package, Url, cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        _downloadService.CancelTaskAsync().Wait();
    }

    public void Pause()
    {
        _downloadService.Pause();
    }

    public void Resume()
    {
        _downloadService.Resume();
    }

    public override bool Equals(object obj)
    {
        return obj is Download download &&
               GetHashCode() == download.GetHashCode();
    }

    public override int GetHashCode()
    {
        int hashCode = 37;
        hashCode = (hashCode * 7) + Url.GetHashCode();
        hashCode = (hashCode * 7) + DownloadedFileSize.GetHashCode();
        return hashCode;
    }

    public async ValueTask DisposeAsync()
    {
        await _downloadService.Clear().ConfigureAwait(false);
        Package = null;
    }

    public void Dispose()
    {
        _downloadService.Clear().Wait();
        Package = null;
    }
}