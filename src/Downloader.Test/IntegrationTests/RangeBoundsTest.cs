namespace Downloader.Test.IntegrationTests;

/// <summary>
/// <see cref="DownloadConfiguration.RangeLow"/> and <see cref="DownloadConfiguration.RangeHigh"/> are
/// inclusive byte indexes.
/// </summary>
public class RangeBoundsTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int FileSize = 1000;

    private static DownloadConfiguration NewConfig(long low, long high) => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        MaxTryAgainOnFailure = 3,
        RangeDownload = true,
        RangeLow = low,
        RangeHigh = high
    };

    [Fact(Timeout = 30_000)]
    public async Task RangePastTheEndStopsAtTheLastByte()
    {
        // arrange
        await using DownloadService service = new(NewConfig(900, 5000));

        // act
        await using Stream stream = await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(FileSize));
        byte[] bytes = ((MemoryStream)stream).ToArray();

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.Equal(100, bytes.Length);
        Assert.Equal(DummyData.GenerateOrderedBytes(FileSize)[900..], bytes);
    }

    [Theory(Timeout = 30_000)]
    [InlineData(500, 100)]
    [InlineData(2000, 3000)]
    [InlineData(1000, 1000)]
    public async Task InvalidRangeFailsTheDownload(long low, long high)
    {
        // arrange
        await using DownloadService service = new(NewConfig(low, high));
        AsyncCompletedEventArgs completed = null;
        service.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(FileSize));

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.IsType<ArgumentOutOfRangeException>(completed?.Error);
    }
}
