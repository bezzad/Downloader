using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader;

/// <summary>
/// Interface of download service which provide all downloader operations
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Gets a value indicating whether the download operation is currently in progress.
    /// </summary>
    bool IsBusy { get; }

    /// <summary>
    /// Gets a value indicating whether the download operation has been cancelled.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Gets the DownloadPackage object that contains information about the file to download.
    /// </summary>
    DownloadPackage Package { get; }

    /// <summary>
    /// Gets the current status of the download operation as a DownloadStatus enum value.
    /// </summary>
    DownloadStatus Status { get; }

    /// <summary>
    /// Event that is raised when the download operation is completed.
    /// The event handler is passed an AsyncCompletedEventArgs object that contains 
    /// information about the completion status of the operation.
    /// </summary>
    event EventHandler<AsyncCompletedEventArgs> DownloadFileCompleted;

    /// <summary>
    /// Event that is raised periodically during the download operation to report the progress of the download.
    /// The event handler is passed a DownloadProgressChangedEventArgs object that contains 
    /// information about the progress of the download, such as the number of bytes downloaded and the total file size.
    /// </summary>
    event EventHandler<DownloadProgressChangedEventArgs> DownloadProgressChanged;

    /// <summary>
    /// Event that is raised periodically during the download operation to report the progress of a single chunk download.
    /// The event handler is passed a DownloadProgressChangedEventArgs object that contains 
    /// information about the progress of the chunk download, such as the number of bytes downloaded and the total chunk size.
    /// </summary>
    event EventHandler<DownloadProgressChangedEventArgs> ChunkDownloadProgressChanged;

    /// <summary>
    /// Event that is raised when the download operation starts.
    /// The event handler is passed a DownloadStartedEventArgs object that contains 
    /// information about the download operation, such as the download URL and the local file path.
    /// </summary>
    event EventHandler<DownloadStartedEventArgs> DownloadStarted;

    /// <summary>
    /// Asynchronously resume downloads a file and returns a Stream object that contains the downloaded file data.
    /// </summary>
    /// <param name="package">The DownloadPackage object that contains information about the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task<Stream> DownloadFileTaskAsync(DownloadPackage package, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously resume downloads a file from the specified URL and returns a Stream object that contains the downloaded file data.
    /// </summary>
    /// <param name="package">The DownloadPackage object that contains information about the file to download.</param>
    /// <param name="address">The download URL of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously resume downloads a file from the specified URL and returns a Stream object that contains the downloaded file data.
    /// </summary>
    /// <param name="package">The DownloadPackage object that contains information about the file to download.</param>
    /// <param name="urls">The download URLs of a file to download as parallel with mirror links.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task<Stream> DownloadFileTaskAsync(DownloadPackage package, string[] urls, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and returns a Stream object that contains the downloaded file data.
    /// </summary>
    /// <param name="address">The download URL of the file to download.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task<Stream> DownloadFileTaskAsync(string address, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and returns a Stream object that contains the downloaded file data.
    /// </summary>
    /// <param name="urls">The download URLs of a file to download as parallel with mirror links.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task<Stream> DownloadFileTaskAsync(string[] urls, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and saves it to the specified file name.
    /// </summary>
    /// <param name="address">The download URL of the file to download.</param>
    /// <param name="fileName">The local file name to save the downloaded file as.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task DownloadFileTaskAsync(string address, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and saves it to the specified file name.
    /// </summary>
    /// <param name="urls">The download URLs of a file to download as parallel with mirror links.</param>
    /// <param name="fileName">The local file name to save the downloaded file as.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task DownloadFileTaskAsync(string[] urls, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and saves it to the specified directory.
    /// </summary>
    /// <param name="address">The download URL of the file to download.</param>
    /// <param name="folder">The local directory to save the downloaded file in.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task DownloadFileTaskAsync(string address, DirectoryInfo folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously downloads a file from the specified URL and saves it to the specified directory.
    /// </summary>
    /// <param name="urls">The download URLs of a file to download as parallel with mirror links.</param>
    /// <param name="folder">The local directory to save the downloaded file in.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the download operation.</param>
    /// <returns>A Task object that represents the asynchronous download operation.</returns>
    Task DownloadFileTaskAsync(string[] urls, DirectoryInfo folder, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the current download operation asynchronously.
    /// </summary>
    void CancelAsync();

    /// <summary>
    /// Cancels the current download operation asynchronously and returns a Task object that represents the cancellation operation.
    /// </summary>
    /// <returns>A Task object that represents the asynchronous cancellation operation.</returns>
    Task CancelTaskAsync();

    /// <summary>
    /// Pauses the current download operation. In this way, you can resume the download very quickly.
    /// </summary>
    /// <remarks>
    /// Note: Please use the cancel method instead of this method 
    /// if you want to stop and dispose of the download and save the download package until you can resume it again.
    /// </remarks>
    void Pause();

    /// <summary>
    /// Resumes a paused download operation.
    /// </summary>
    void Resume();

    /// <summary>
    /// Clears any data related to the current download operation.
    /// </summary>
    /// <returns>A Task object that represents the asynchronous clearing operation.</returns>
    Task Clear();

    /// <summary>
    /// Add logger class to log the Downloader events
    /// </summary>
    /// <param name="logger"></param>
    void AddLogger(ILogger logger);
}