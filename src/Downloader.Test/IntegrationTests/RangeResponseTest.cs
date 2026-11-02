namespace Downloader.Test.IntegrationTests;

/// <summary>
/// A server that says it supports ranges but sends the whole file must not give a broken file.
/// </summary>
public class RangeResponseTest(ITestOutputHelper output) : BaseTestClass(output)
{
    // Not a multiple of 256: ordered bytes repeat every 256 bytes, so a chunk of such a size would
    // hide a wrong offset.
    private const int Size = 10_000;
    private static readonly byte[] Expected = DummyData.GenerateOrderedBytes(Size);

    private static DownloadConfiguration NewConfig() => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0
    };

    [Fact(Timeout = 60_000)]
    public async Task ServerThatIgnoresRangeGivesTheRightFile()
    {
        // arrange
        string url = DummyFileHelper.GetFileIgnoringRangeUrl("file.bin", Size);
        await using DownloadService service = new(NewConfig());

        // act
        await using Stream result = await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.True(Expected.AreEqual(result));
    }

    [Fact(Timeout = 60_000)]
    public async Task MirrorThatIgnoresRangeDoesNotBreakTheFile()
    {
        // arrange
        string[] urls = [
            DummyFileHelper.GetFileUrl(Size),
            DummyFileHelper.GetFileIgnoringRangeUrl("file.bin", Size)
        ];
        await using DownloadService service = new(NewConfig());

        // act
        await using Stream result = await service.DownloadFileTaskAsync(urls);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.True(Expected.AreEqual(result));
    }

    [Fact(Timeout = 60_000)]
    public async Task RangeDownloadFromServerThatIgnoresRangeFails()
    {
        // arrange
        DownloadConfiguration config = NewConfig();
        config.RangeDownload = true;
        config.RangeLow = 100;
        config.RangeHigh = 199;
        string url = DummyFileHelper.GetFileIgnoringRangeUrl("file.bin", Size);
        await using DownloadService service = new(config);
        AsyncCompletedEventArgs completed = null;
        service.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.IsType<Downloader.Exceptions.InvalidRangeResponseException>(completed?.Error);
    }
}
