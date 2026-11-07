namespace Downloader.Test.Helper;

/// <summary>
/// Helpers for tests that read <see cref="IDownloadService.DownloadProgressChanged"/>.
/// </summary>
public static class ProgressHelper
{
    /// <summary>
    /// The whole-file progress right now, read from the chunks. A test that stops a download "at X%" reads
    /// it in <see cref="IDownloadService.ChunkDownloadProgressChanged"/>, which runs on the chunk thread:
    /// <see cref="IDownloadService.DownloadProgressChanged"/> does not hold the download (issue #256), so
    /// on a fast local server it can arrive after the download is over.
    /// </summary>
    public static double ReceivedPercent(AbstractDownloadService service)
    {
        long total = service.Package.TotalFileSize;
        return total == 0 ? 0 : service.Package.ReceivedBytesSize * 100.0 / total;
    }

    /// <summary>
    /// Waits until every DownloadProgressChanged update so far has reached the handlers. The download
    /// does not wait for them, so the last updates can arrive after the awaited download returns.
    /// </summary>
    public static async Task WaitForProgressEvents(AbstractDownloadService service)
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (!service.IsProgressDelivered && watch.Elapsed < TimeSpan.FromSeconds(30))
            await Task.Delay(10);

        Assert.True(service.IsProgressDelivered, "The progress updates were not delivered in 30 seconds.");
    }
}
