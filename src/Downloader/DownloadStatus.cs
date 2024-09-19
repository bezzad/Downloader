namespace Downloader;

/// <summary>
/// Represents the status of a download operation.
/// </summary>
public enum DownloadStatus
{
    /// <summary>
    /// The download has not started.
    /// </summary>
    None = 0,

    /// <summary>
    /// The download has been created but not yet started.
    /// </summary>
    Created = 1,

    /// <summary>
    /// The download is currently running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// The download has been stopped or cancelled.
    /// </summary>
    Stopped = 3,

    /// <summary>
    /// The download has been paused.
    /// </summary>
    Paused = 4,

    /// <summary>
    /// The download has completed successfully.
    /// </summary>
    Completed = 5,

    /// <summary>
    /// The download has failed.
    /// </summary>
    Failed = 6
}