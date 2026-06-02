namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// Integration test for GitHub issue #231 (follow-up):
/// In some environments a TLS-inspecting proxy/antivirus breaks concurrent HTTPS connections, so
/// parallel/range chunk downloads fail (SEC_E_DECRYPT_FAILURE, "response ended prematurely",
/// aborted sockets) even though a single sequential connection works. The download service must
/// automatically fall back to a single connection and complete, instead of failing the download.
/// </summary>
[Collection("Sequential")]
public class Issue231Test(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact(Timeout = 60_000)]
    public async Task ParallelDownloadFallsBackToSingleConnectionWhenRangeRequestsFail()
    {
        // arrange — the server fails every parallel/range chunk request (503) but serves the full
        // file to a single no-Range request, mimicking an environment that breaks concurrent
        // connections while a single connection works.
        const int size = 1024 * 1024; // 1MB → splits into multiple parallel chunks
        string filename = Path.GetRandomFileName();
        string url = DummyFileHelper.GetFileFailingOnRangeRequestsUrl(filename, size);
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            MinimumSizeOfChunking = 0, // allow the 1MB file to be chunked
            MaxTryAgainOnFailure = 2,  // give up the parallel attempt quickly, then fall back
            BlockTimeout = 100         // keep the retry backoff short for the test
        };
        await using DownloadService downloader = new(config, LogFactory);
        AsyncCompletedEventArgs completed = null;
        downloader.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        // assert — the single-connection fallback must complete the download successfully.
        Assert.NotNull(completed);
        Assert.Null(completed.Error);
        Assert.False(completed.Cancelled);
        Assert.Equal(DownloadStatus.Completed, downloader.Package.Status);
        Assert.True(downloader.Package.IsSaveComplete);
        Assert.NotNull(stream);
        Assert.Equal(size, stream.Length);
        Assert.True(DummyData.GenerateOrderedBytes(size).AreEqual(stream),
            "Downloaded bytes do not match expected content");
    }
}
