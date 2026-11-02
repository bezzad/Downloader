using Downloader.Test.Helper;

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

    [Theory(Timeout = 60_000)]
    [InlineData(false)] // serial: one connection at a time
    [InlineData(true)]  // parallel: the server sees more than one, so the counter really works
    public async Task ServerSeesOneOpenRequestInASerialDownload(bool parallel)
    {
        // arrange
        using CountingRangeServer server = new(DummyData.GenerateOrderedBytes(10_000));
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = parallel,
            MinimumSizeOfChunking = 0,
            BufferBlockSize = 1024
        };
        await using DownloadService service = new(config);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(server.Url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(server.Body, ((MemoryStream)result).ToArray());
        if (parallel)
            Assert.True(server.MaxOpenRequests > 1, $"max open requests: {server.MaxOpenRequests}");
        else
            Assert.Equal(1, server.MaxOpenRequests);
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
