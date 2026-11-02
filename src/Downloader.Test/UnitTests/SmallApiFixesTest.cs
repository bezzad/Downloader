using Downloader.Exceptions;

namespace Downloader.Test.UnitTests;

public class SmallApiFixesTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact(Timeout = 60_000)]
    public async Task BuilderWithFileNameOnlyDownloadsToThatName()
    {
        // arrange
        string file = Path.GetTempFileName(); // full path, no folder given
        File.Delete(file);
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        IDownload download = DownloadBuilder.New().WithUrl(url).WithFileName(file).Build();

        try
        {
            // act
            await download.StartAsync();

            // assert
            Assert.Equal(DownloadStatus.Completed, download.Status);
            byte[] bytes = await File.ReadAllBytesAsync(file);
            Assert.Equal(DummyFileHelper.File16Kb, bytes);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void BuildWithoutUrlNamesTheUrl()
    {
        // act
        var error = Assert.Throws<ArgumentNullException>(() => DownloadBuilder.New().Build());

        // assert
        Assert.Equal("url", error.ParamName);
        Assert.Contains("WithUrl", error.Message);
    }

    [Fact]
    public void FileExistExceptionMessageNamesTheFile()
    {
        // act
        var error = new FileExistException("/tmp/a.zip");

        // assert
        Assert.Equal("/tmp/a.zip", error.Name);
        Assert.Contains("/tmp/a.zip", error.Message);
    }

    [Fact(Timeout = 30_000)]
    public async Task CancelDoesNotWaitForTheSpeedLimit()
    {
        // arrange
        await using ThrottledStream stream = new(new MemoryStream(new byte[1024 * 1024]), 1024);
        byte[] buffer = new byte[1024 * 64]; // 64 seconds of data at 1 KB/s
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(300));
        Stopwatch watch = Stopwatch.StartNew();

        // act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            while (true)
                _ = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
        });

        // assert
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }
}
