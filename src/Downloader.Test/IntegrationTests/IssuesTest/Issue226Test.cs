namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Integration tests for GitHub issue #226:
/// "In the AOT environment, some links cannot be downloaded."
///
/// In AOT builds the assembly version can be resolved as 0.0.0 / 0.0.0.0, causing
/// RequestConfiguration.UserAgent to default to "Downloader/0.0.0.0".  Some CDN
/// servers respond with HTTP 428 (Precondition Required) to such User-Agent strings.
/// The fix in SocketClient.ResolveUserAgent() normalises invalid UA values to a
/// valid fallback before sending, so the download succeeds transparently.
/// </summary>
[Collection("Sequential")]
public class Issue226Test(ITestOutputHelper output) : BaseTestClass(output)
{
    private static readonly int FileSize = DummyFileHelper.FileSize16Kb;
    private static readonly byte[] FileData = DummyFileHelper.File16Kb;

    // All values that an AOT build (or misconfigured client) may produce as User-Agent
    public static IEnumerable<object[]> InvalidAotUserAgents =>
    [
        [null],                  // no UA set
        [""],                    // empty string
        ["Downloader/"],         // trailing slash – version stripped by trimmer
        ["Downloader/0.0.0"],    // 3-part zero version from AOT build
        ["Downloader/0.0.0.0"],  // 4-part zero version from AOT build
    ];

    /// <summary>
    /// Verifies that a download succeeds even when the RequestConfiguration carries
    /// one of the invalid User-Agent values produced by AOT builds.
    /// The server at the target URL returns HTTP 428 for any invalid UA, so a
    /// regression (i.e. the fix being removed) would cause this test to fail.
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidAotUserAgents))]
    public async Task DownloadSucceedsWhenUserAgentIsInvalidAotValue(string userAgent)
    {
        // arrange
        string filename = Path.GetRandomFileName();
        string url = DummyFileHelper.GetFileRequiringValidUserAgentUrl(filename, FileSize);
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            RequestConfiguration = new RequestConfiguration {
                UserAgent = userAgent  // simulate AOT zero-version or empty UA
            }
        };
        using DownloadService downloader = new(config, LogFactory);
        bool succeeded = false;
        Exception downloadError = null;
        downloader.DownloadFileCompleted += (_, e) => {
            succeeded = e.Error == null && !e.Cancelled;
            downloadError = e.Error;
        };

        // act
        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        // assert – download must complete without a 428 or any other error
        Assert.True(succeeded, $"Download failed with UserAgent='{userAgent}': {downloadError?.Message}");
        Assert.NotNull(stream);
        Assert.Equal(FileSize, stream.Length);
        Assert.True(FileData.AreEqual(stream), "Downloaded bytes do not match expected content");
    }

    /// <summary>
    /// Verifies that a custom, valid User-Agent is preserved exactly as supplied
    /// and that the download still succeeds (positive control for issue #226).
    /// </summary>
    [Theory]
    [InlineData("MyApp/1.0")]
    [InlineData("CustomDownloader/3.2.1")]
    [InlineData("Downloader/5.5.0")]
    public async Task DownloadSucceedsWhenUserAgentIsValidCustomValue(string userAgent)
    {
        // arrange
        string filename = Path.GetRandomFileName();
        string url = DummyFileHelper.GetFileRequiringValidUserAgentUrl(filename, FileSize);
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            RequestConfiguration = new RequestConfiguration {
                UserAgent = userAgent
            }
        };
        using DownloadService downloader = new(config, LogFactory);
        bool succeeded = false;
        downloader.DownloadFileCompleted += (_, e) => succeeded = e.Error == null && !e.Cancelled;

        // act
        await using Stream stream = await downloader.DownloadFileTaskAsync(url);

        // assert
        Assert.True(succeeded, $"Download failed with UserAgent='{userAgent}'");
        Assert.NotNull(stream);
        Assert.Equal(FileSize, stream.Length);
    }
}
