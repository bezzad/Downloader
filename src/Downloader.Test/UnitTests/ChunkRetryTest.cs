namespace Downloader.Test.UnitTests;

public class ChunkRetryTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 1024;

    private static async Task<(ChunkDownloader, ConcurrentStream)> NewDownloader(Chunk chunk)
    {
        DownloadConfiguration config = new() { BlockTimeout = 100, ChunkCount = 1 };
        ConcurrentStream storage = new();
        SocketClient client = new(config);
        // Fill the client's header cache from a working URL, so a retry does not probe the failing one.
        await client.IsSupportDownloadInRange(new Request(DummyFileHelper.GetFileUrl(Size)), CancellationToken.None);
        return (new ChunkDownloader(chunk, config, storage, client), storage);
    }

    private static Request FailingRequest(int failures) =>
        new(DummyFileHelper.GetFileAfterFailuresUrl(Guid.NewGuid().ToString("N"), Size, failures, 503));

    [Fact(Timeout = 60_000)]
    public async Task ManyRetriesInARowStillFinish()
    {
        // arrange
        Chunk chunk = new(0, Size - 1) { Timeout = 5000, MaxTryAgainOnFailure = int.MaxValue };
        (ChunkDownloader downloader, ConcurrentStream storage) = await NewDownloader(chunk);
        await using ConcurrentStream _ = storage;

        // act
        await downloader.Download(FailingRequest(6), new PauseTokenSource().Token, CancellationToken.None);
        await storage.FlushAsync();

        // assert
        Assert.Equal(6, chunk.FailureCount);
        Assert.Equal(Size, chunk.Position);
        Assert.Equal(DummyData.GenerateOrderedBytes(Size), storage.Data);
    }

    [Fact(Timeout = 60_000)]
    public async Task ReadTimeoutGrowthIsCapped()
    {
        // arrange: a timeout near int.MaxValue used to overflow to a negative value on the next retry
        Chunk chunk = new(0, Size - 1) { Timeout = int.MaxValue - 10, MaxTryAgainOnFailure = int.MaxValue };
        (ChunkDownloader downloader, ConcurrentStream storage) = await NewDownloader(chunk);
        await using ConcurrentStream _ = storage;

        // act
        await downloader.Download(FailingRequest(1), new PauseTokenSource().Token, CancellationToken.None);

        // assert
        Assert.Equal(Size, chunk.Position);
        Assert.InRange(chunk.Timeout, 100, 60_000);
    }
}
