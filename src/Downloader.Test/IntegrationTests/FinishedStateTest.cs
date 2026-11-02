namespace Downloader.Test.IntegrationTests;

/// <summary>
/// A finished download keeps its status, and <see cref="AbstractDownloadService.Clear"/> does not wait
/// for a download that is still probing the server.
/// </summary>
public class FinishedStateTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static DownloadConfiguration NewConfig() => new() {
        ChunkCount = 2,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        MaxTryAgainOnFailure = 0
    };

    private static void CallEveryControl(DownloadService service)
    {
        service.Pause();
        service.Resume();
        service.CancelAsync();
        service.Pause();
    }

    [Fact(Timeout = 30_000)]
    public async Task CompletedDownloadKeepsItsStatus()
    {
        // arrange
        await using DownloadService service = new(NewConfig());
        await using Stream stream = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb));

        // act
        CallEveryControl(service);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.False(service.IsBusy);
        Assert.False(service.IsPaused);
    }

    [Fact(Timeout = 30_000)]
    public async Task FailedDownloadKeepsItsStatus()
    {
        // arrange
        await using DownloadService service = new(NewConfig());
        string url = DummyFileHelper.GetFileWithNameUrl("file.bin", DummyFileHelper.FileSize1Kb).Replace("/dummyfile/", "/no-such-path/");
        await service.DownloadFileTaskAsync(url);
        Assert.Equal(DownloadStatus.Failed, service.Status);

        // act
        CallEveryControl(service);

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.False(service.IsBusy);
    }

    [Fact(Timeout = 30_000)]
    public async Task PauseWhileStartingIsKept()
    {
        // arrange: the server waits before it answers, so the download is still starting (Created)
        await using DownloadService service = new(NewConfig());
        string url = DummyFileHelper.GetFileWithSlowHeadersUrl(DummyFileHelper.FileSize1Kb, 1_000);
        Task<Stream> download = service.DownloadFileTaskAsync(url);
        while (service.Status != DownloadStatus.Created)
            await Task.Delay(10);

        // act
        service.Pause();

        // assert: the pause is not lost, and the download waits for Resume
        Assert.Equal(DownloadStatus.Paused, service.Status);
        Assert.True(service.IsPaused);
        await Task.Delay(3_000); // the headers came long ago; only the pause holds the chunks now
        Assert.False(download.IsCompleted);

        service.Resume();
        await using Stream stream = await download;
        Assert.Equal(DownloadStatus.Completed, service.Status);
    }

    [Fact(Timeout = 30_000)]
    public async Task ClearWhileProbingStopsTheDownloadQuickly()
    {
        // arrange
        DownloadService service = new(NewConfig());
        string url = DummyFileHelper.GetFileWithSlowHeadersUrl(DummyFileHelper.FileSize1Kb, 20_000);
        Task download = service.DownloadFileTaskAsync(url);
        while (service.Status != DownloadStatus.Created)
            await Task.Delay(10);
        await Task.Delay(200); // the header probe is running now

        // act
        Stopwatch watch = Stopwatch.StartNew();
        await service.Clear();
        watch.Stop();
        await download;

        // assert
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Clear took {watch.Elapsed}");
        Assert.Equal(DownloadStatus.Stopped, service.Status);
        await service.DisposeAsync();
    }
}
