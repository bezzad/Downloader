using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Represents an interface for managing file downloads.
/// </summary>
public interface IDownload : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the URL of the file to be downloaded.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the folder where the downloaded file will be saved.
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// Gets the name of the file to be saved.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    /// Gets the size of the downloaded portion of the file.
    /// </summary>
    public long DownloadedFileSize { get; }

    /// <summary>
    /// Gets the total size of the file to be downloaded.
    /// </summary>
    public long TotalFileSize { get; }

    /// <summary>
    /// Gets the download package containing information about the download.
    /// </summary>
    public DownloadPackage Package { get; }

    /// <summary>
    /// Gets the current status of the download.
    /// </summary>
    public DownloadStatus Status { get; }

    /// <summary>
    /// Occurs when the progress of a chunk download changes.
    /// </summary>
    public event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;

    /// <summary>
    /// Occurs when the download file operation is completed.
    /// </summary>
    public event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;

    /// <summary>
    /// Occurs when the overall download progress changes.
    /// </summary>
    public event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    /// <summary>
    /// Occurs when the download operation starts.
    /// </summary>
    public event EventHandler<DownloadStartedEventArgs> DownloadStarted;

    /// <summary>
    /// Starts the download operation asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download.</param>
    /// <returns>A task that represents the asynchronous download operation. The task result contains the downloaded stream.</returns>
    public Task<Stream> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the download operation.
    /// </summary>
    public void Stop();

    /// <summary>
    /// Pauses the download operation.
    /// </summary>
    public void Pause();

    /// <summary>
    /// Resumes the paused download operation.
    /// </summary>
    public void Resume();
}