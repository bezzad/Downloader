namespace Downloader.Test.UnitTests;

public class SocketClientTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly SocketClient _socketClient = new(new RequestConfiguration());

    [Fact]
    public async Task GetUrlDispositionWhenNoUrlFileNameTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(DummyFileHelper.SampleFile1KbName,
            DummyFileHelper.FileSize1Kb);
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Equal(DummyFileHelper.SampleFile1KbName, actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenNoUrlTest()
    {
        // arrange
        string url = "  ";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenBadUrlTest()
    {
        // arrange
        string url = "http://www.a.com/a/b/c/d/e/";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenBadUrlWithFilenameTest()
    {
        // arrange
        string filename = "test";
        string url = "http://www.a.com/a/b/c/" + filename;
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameTest()
    {
        // arrange
        string filename = "test.xml";
        string url = filename;
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameWithoutExtensionTest()
    {
        // arrange
        string filename = "test";
        string url = filename;
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithShortUrlTest()
    {
        // arrange
        string filename = "test.xml";
        string url = "/" + filename;
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }


    [Fact]
    public async Task GetUrlDispositionWithShortUrlAndQueryParamTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"/{filename}?q=1";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithShortUrlAndQueryParamsTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"/{filename}?q=1&x=100.0&y=testName";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameAndQueryParamsTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"{filename}?q=1&x=100.0&y=testName";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithUrlAndQueryParamsTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"http://www.a.com/{filename}?q=1&x=1&filename=test";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithUrlAndQueryParamsAndFragmentIdentifierTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithLongUrlTest()
    {
        // arrange
        string filename = "excel_sample.xls";
        string url =
            $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{filename}?test=1";
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request);

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetFileNameOnRedirectUrlTest()
    {
        // arrange
        string filename = "test.zip";
        string url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);
        Request request = new(url);

        // act
        await _socketClient.SetRequestFileNameAsync(request);

        // assert
        Assert.Equal(filename, request.FileName);
    }

    [Fact]
    public void GetRedirectUrlByLocationTest()
    {
        // arrange
        string filename = "test.zip";
        string url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);
        string redirectUrl = DummyFileHelper.GetFileWithNameUrl(filename, DummyFileHelper.FileSize1Kb);
        Request request = new(url);
        HttpResponseMessage response = new();
        response.Headers.Location = new Uri(redirectUrl);

        // act
        Uri actualRedirectUrl = _socketClient.GetRedirectUrl(response);

        // assert
        Assert.NotEqual(url, redirectUrl);
        Assert.NotEqual(request.Address.ToString(), redirectUrl);
        Assert.Equal(redirectUrl, actualRedirectUrl.AbsoluteUri);
    }

    [Fact]
    public async Task GetRedirectUrlWithoutLocationTest()
    {
        // arrange
        string filename = "test.zip";
        string url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);
        string redirectUrl = DummyFileHelper.GetFileWithNameUrl(filename, DummyFileHelper.FileSize1Kb);
        Request request = new(url);
        HttpRequestMessage msg = request.GetRequest();

        // act
        HttpResponseMessage resp = await _socketClient.SendRequestAsync(msg);
        Uri actualRedirectUrl = _socketClient.GetRedirectUrl(resp);

        // assert
        Assert.NotEqual(url, redirectUrl);
        Assert.NotEqual(request.Address.ToString(), redirectUrl);
        Assert.Equal(redirectUrl, actualRedirectUrl.AbsoluteUri);
    }

    [Fact]
    public async Task GetFileSizeTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        int expectedSize = DummyFileHelper.FileSize16Kb;
        Request request = new(url);

        // act
        long actualSize = await _socketClient.GetFileSizeAsync(request);

        // assert
        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public async Task IsSupportDownloadInRangeTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Request request = new(url);

        // act
        bool actualCan = await _socketClient.IsSupportDownloadInRange(request);

        // assert
        Assert.True(actualCan);
    }

    [Fact]
    public void GetTotalSizeFromContentLengthTest()
    {
        // arrange
        const long length = 23432L;
        Dictionary<string, string> headers = new() { { "Content-Length", length.ToString() } };

        // act
        long actualLength = _socketClient.GetTotalSizeFromContentLength(headers);

        // assert
        Assert.Equal(length, actualLength);
    }

    [Fact]
    public void GetTotalSizeFromContentLengthWhenNoHeaderTest()
    {
        // arrange
        int length = -1;
        Dictionary<string, string> headers = new();

        // act
        long actualLength = _socketClient.GetTotalSizeFromContentLength(headers);

        // assert
        Assert.Equal(length, actualLength);
    }


    [Fact]
    public void GetTotalSizeFromContentRangeTest()
    {
        TestGetTotalSizeFromContentRange(23432, "bytes 0-0/23432");
    }

    [Fact]
    public void GetTotalSizeFromContentRangeWhenUnknownSizeTest()
    {
        TestGetTotalSizeFromContentRange(-1, "bytes 0-1000/*");
    }

    [Fact]
    public void GetTotalSizeFromContentRangeWhenUnknownRangeTest()
    {
        TestGetTotalSizeFromContentRange(23529, "bytes */23529");
    }

    [Fact]
    public void GetTotalSizeFromContentRangeWhenIncorrectTest()
    {
        TestGetTotalSizeFromContentRange(23589, "bytes -0/23589");
    }

    [Fact]
    public void GetTotalSizeFromContentRangeWhenNoHeaderTest()
    {
        TestGetTotalSizeFromContentRange(-1, null);
    }

    private void TestGetTotalSizeFromContentRange(long expectedLength, string contentRange)
    {
        // arrange
        Dictionary<string, string> headers = new();
        if (string.IsNullOrEmpty(contentRange) == false)
            headers["Content-Range"] = contentRange;

        // act
        long actualLength = _socketClient.GetTotalSizeFromContentRange(headers);

        // assert
        Assert.Equal(expectedLength, actualLength);
    }
}