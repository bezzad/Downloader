using System.Net.Http;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// The configuration the caller gives is input. A download must not change it.
/// </summary>
public class CallerConfigUnchangedTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static DownloadConfiguration NewConfig() => new() {
        ChunkCount = 8,
        ParallelCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 1024
    };

    private static void AssertUnchanged(DownloadConfiguration config)
    {
        Assert.Equal(8, config.ChunkCount);
        Assert.Equal(4, config.ParallelCount);
        Assert.True(config.ParallelDownload);
    }

    [Fact(Timeout = 60_000)]
    public async Task SmallFileDoesNotChangeTheConfig()
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        await using DownloadService service = new(config);

        // act: 100 bytes is below MinimumSizeOfChunking, so this download uses one chunk
        await using Stream result = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(100));

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        AssertUnchanged(config);
    }

    [Fact(Timeout = 60_000)]
    public async Task BigFileAfterASmallFileUsesAllChunks()
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        await using DownloadService service = new(config);
        await using (await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(100))) { }

        // act
        await using Stream result = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb));

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(8, service.Plan.ChunkCount);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(result));
    }

    [Fact(Timeout = 60_000)]
    public async Task ServerWithoutRangeDoesNotChangeTheConfig()
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        await using DownloadService service = new(config);
        string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl("file.bin", DummyFileHelper.FileSize16Kb);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(1, service.Plan.ChunkCount);
        AssertUnchanged(config);
    }

    [Theory(Timeout = 60_000)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RangeValuesAreKept(bool rangeDownload)
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        config.RangeDownload = rangeDownload;
        config.RangeLow = 10;
        config.RangeHigh = 100_000; // past the end, the download uses the last byte
        await using DownloadService service = new(config);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb));

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(10, config.RangeLow);
        Assert.Equal(100_000, config.RangeHigh);
    }

    [Fact(Timeout = 60_000)]
    public async Task HttpClientFactoryThatReturnsNullIsKept()
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        Func<HttpClient> factory = () => null;
        config.CustomHttpClientFactory = factory;
        await using DownloadService service = new(config);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb));

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Same(factory, config.CustomHttpClientFactory);
    }
}
