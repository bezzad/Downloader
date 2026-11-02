namespace Downloader.Test.IntegrationTests;

/// <summary>
/// A serial download uses one connection, and chunks stop listening to a shared configuration.
/// </summary>
public class SerialAndConfigHandlerTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static int PropertyChangedHandlerCount(DownloadConfiguration config)
    {
        FieldInfo field = typeof(DownloadConfiguration).GetField(nameof(DownloadConfiguration.PropertyChanged),
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (field!.GetValue(config) as Delegate)?.GetInvocationList().Length ?? 0;
    }

    [Fact(Timeout = 60_000)]
    public async Task SerialDownloadRunsOneChunkAtATime()
    {
        // arrange
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = false,
            MinimumSizeOfChunking = 0,
            BufferBlockSize = 1024
        };
        await using DownloadService service = new(config);
        int maxActiveChunks = 0;
        service.DownloadProgressChanged += (_, e) => maxActiveChunks = Math.Max(maxActiveChunks, e.ActiveChunks);
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb * 8);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(DummyFileHelper.FileSize16Kb * 8, result.Length);
        Assert.Equal(1, maxActiveChunks);
    }

    [Fact(Timeout = 60_000)]
    public async Task ChunksStopListeningToTheSharedConfiguration()
    {
        // arrange
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelDownload = true,
            MinimumSizeOfChunking = 0
        };
        int handlersBefore = PropertyChangedHandlerCount(config);
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);

        // act
        for (int i = 0; i < 3; i++)
        {
            await using DownloadService service = new(config);
            await using Stream result = await service.DownloadFileTaskAsync(url);
            Assert.Equal(DownloadStatus.Completed, service.Status);
        }

        // assert
        Assert.Equal(handlersBefore, PropertyChangedHandlerCount(config));
    }
}
