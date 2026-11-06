namespace Downloader;

/// <summary>
/// The chunk and range values one download really uses. They start as the caller's
/// <see cref="DownloadConfiguration"/> values, and the download may change them (for example to one
/// connection for a small file) without changing the caller's configuration.
/// </summary>
internal sealed class DownloadPlan
{
    public int ChunkCount { get; set; }
    public int ParallelCount { get; set; }
    public bool ParallelDownload { get; set; }
    public long RangeLow { get; set; }
    public long RangeHigh { get; set; }
    public bool LiveStreaming { get; init; }

    public static DownloadPlan From(DownloadConfiguration config) => new() {
        ChunkCount = config.ChunkCount,
        ParallelCount = config.ParallelCount,
        ParallelDownload = config.ParallelDownload,
        RangeLow = config.RangeLow,
        RangeHigh = config.RangeHigh,
        LiveStreaming = config.EnableLiveStreaming
    };
}
