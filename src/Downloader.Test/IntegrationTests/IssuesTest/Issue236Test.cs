namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// Integration test for GitHub issue #236:
/// When a caller supplies a custom <see cref="HttpClient"/> whose handler has
/// <c>AutomaticDecompression</c> enabled, a server that returns gzip-compressed content delivers
/// more decompressed bytes than the (compressed-size) <c>Content-Length</c> it advertised. The
/// download must not silently truncate at the compressed byte count — it must write out the full
/// decompressed content.
/// </summary>
[Collection("Sequential")]
public class Issue236Test(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact(Timeout = 60_000)]
    public async Task DownloadDoesNotTruncateGzipCompressedContentFromCustomHttpClient()
    {
        // arrange — the server serves a gzip-compressed body whose Content-Length reflects only
        // the compressed byte count; a custom HttpClient with automatic decompression transparently
        // delivers the larger decompressed stream.
        const int size = 256 * 1024; // large + low-entropy enough to compress well below its size
        string filename = Path.GetRandomFileName();
        string url = DummyFileHelper.GetGzipCompressedFileUrl(filename, size);
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            MinimumSizeOfChunking = 0,
            CustomHttpClientFactory = () => new HttpClient(new HttpClientHandler {
                AutomaticDecompression = DecompressionMethods.All
            })
        };
        await using DownloadService downloader = new(config, LogFactory);
        AsyncCompletedEventArgs completed = null;
        downloader.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        // assert — the full decompressed content must be written, not truncated at the compressed
        // Content-Length.
        Assert.NotNull(completed);
        Assert.Null(completed.Error);
        Assert.False(completed.Cancelled);
        Assert.Equal(DownloadStatus.Completed, downloader.Package.Status);
        Assert.NotNull(stream);
        Assert.Equal(size, stream.Length);
        Assert.True(DummyData.GenerateOrderedBytes(size).AreEqual(stream),
            "Downloaded (decompressed) bytes do not match expected content");
    }
}
