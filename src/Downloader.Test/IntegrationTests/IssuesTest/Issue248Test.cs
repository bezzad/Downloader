namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// Integration test for GitHub issue #248:
/// When the server answers the first probe with an error (e.g. <c>500</c> and a 41-byte XML body),
/// the error response's headers must not be taken as the file's. Previously its
/// <c>Content-Length</c> became the total file size and the missing <c>Content-Range</c> read as
/// "no range support", so a later successful request produced a 41-byte file reported as Completed.
/// </summary>
[Collection("Sequential")]
public class Issue248Test(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 1024 * 1024;

    [Fact(Timeout = 60_000)]
    public async Task FirstErrorResponseDoesNotTruncateTheFile()
    {
        // arrange — only the first request fails; every later one gets the real file.
        string url = DummyFileHelper.GetFileAfterFailuresUrl(Path.GetRandomFileName(), Size, failures: 1);
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        await using DownloadService downloader = new(new DownloadConfiguration { ChunkCount = 8 }, LogFactory);
        AsyncCompletedEventArgs completed = null;
        downloader.DownloadFileCompleted += (_, e) => completed = e;

        try
        {
            // act
            await downloader.DownloadFileTaskAsync(url, path);

            // assert — the whole file, not the error body's 41 bytes.
            Assert.NotNull(completed);
            Assert.Null(completed.Error);
            Assert.Equal(DownloadStatus.Completed, downloader.Package.Status);
            Assert.Equal(Size, downloader.Package.TotalFileSize);
            // One passing error must not cost the range support: without it the file comes over one
            // connection, and a resume starts again from byte 0.
            Assert.True(downloader.Package.IsSupportDownloadInRange);
            await using FileStream file = File.OpenRead(path);
            Assert.Equal(Size, file.Length);
            Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(file),
                "Downloaded bytes do not match expected content");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task TransientErrorsOnTheProbeAreRetried()
    {
        // arrange — the first four requests get a 503 (two full probes, each with and without Range),
        // then the server is back.
        string url = DummyFileHelper.GetFileAfterFailuresUrl(Path.GetRandomFileName(), Size, failures: 4, status: 503);
        DownloadConfiguration config = new() { ChunkCount = 8, BlockTimeout = 100 };
        await using DownloadService downloader = new(config, LogFactory);
        AsyncCompletedEventArgs completed = null;
        downloader.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        // assert — the probe was tried again, like a chunk would be, instead of failing the download.
        Assert.NotNull(completed);
        Assert.Null(completed.Error);
        Assert.Equal(DownloadStatus.Completed, downloader.Package.Status);
        Assert.True(downloader.Package.IsSupportDownloadInRange);
        Assert.True(DummyData.GenerateOrderedBytes(Size).AreEqual(stream),
            "Downloaded bytes do not match expected content");
    }

    [Fact(Timeout = 60_000)]
    public async Task AlwaysFailingServerEndsFailedNotCompleted()
    {
        // arrange — no request ever reaches the file.
        string url = DummyFileHelper.GetFileAfterFailuresUrl(Path.GetRandomFileName(), Size, failures: int.MaxValue);
        DownloadConfiguration config = new() { ChunkCount = 8, MaxTryAgainOnFailure = 2, BlockTimeout = 100 };
        await using DownloadService downloader = new(config, LogFactory);
        AsyncCompletedEventArgs completed = null;
        downloader.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await downloader.DownloadFileTaskAsync(url);

        // assert
        Assert.NotNull(completed);
        Assert.NotNull(completed.Error);
        Assert.Equal(DownloadStatus.Failed, downloader.Package.Status);
    }
}
