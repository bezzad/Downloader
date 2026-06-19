namespace Downloader.Test.UnitTests;

public class RemoteFileResolverTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public async Task GetFileNameFromContentDispositionTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(DummyFileHelper.SampleFile1KbName,
            DummyFileHelper.FileSize1Kb);

        // act
        string filename = await RemoteFileResolver.GetFileNameAsync(url, CancellationToken.None);

        // assert
        Assert.Equal(DummyFileHelper.SampleFile1KbName, filename);
    }

    [Fact]
    public async Task GetFileNameFallsBackToUrlPathTest()
    {
        // arrange: server sends no Content-Disposition, so the name comes from the URL path
        string url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName,
            DummyFileHelper.FileSize16Kb);

        // act
        string filename = await RemoteFileResolver.GetFileNameAsync(url, CancellationToken.None);

        // assert
        Assert.Equal(DummyFileHelper.SampleFile16KbName, filename);
    }

    [Fact]
    public async Task GetFileNameOnUnreachableHostFallsBackToUrlNameTest()
    {
        // arrange: resolution must not throw on a network error — it falls back to the URL name
        string url = "https://an.unreachable.invalid.host.example/path/movie.mkv";

        // act
        string filename = await RemoteFileResolver.GetFileNameAsync(url, CancellationToken.None);

        // assert
        Assert.Equal("movie.mkv", filename);
    }

    [Fact]
    public async Task GetFileInfoResolvesNameAndSizeTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(DummyFileHelper.SampleFile1KbName,
            DummyFileHelper.FileSize1Kb);

        // act
        RemoteFileInfo info = await RemoteFileResolver.GetFileInfoAsync(url, CancellationToken.None);

        // assert
        Assert.Equal(DummyFileHelper.SampleFile1KbName, info.FileName);
        Assert.Equal(DummyFileHelper.FileSize1Kb, info.FileSize);
        Assert.True(info.SupportsRange);
        Assert.NotNull(info.Address);
    }

    [Fact]
    public async Task GetFileNameThrowsOnEmptyUrlTest()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            RemoteFileResolver.GetFileNameAsync("  ", CancellationToken.None));
    }
}
