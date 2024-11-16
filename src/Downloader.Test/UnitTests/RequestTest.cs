namespace Downloader.Test.UnitTests;

public class RequestTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const string EnglishText = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string PersianText = "۰۱۲۳۴۵۶۷۸۹ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیًٌٍَُِّْؤئيإأآة»«:؛كٰ‌ٔء؟";
    private static readonly Encoding Latin1Encoding = Encoding.GetEncoding("iso-8859-1");

    [Theory]
    [InlineData("    ",  "")] // When No Url
    [InlineData("http://www.a.com/a/b/c/d/e/",  "")] // When Bad Url
    [InlineData("http://www.a.com/a/b/c/filename", "filename")] // When bad Url with filename
    [InlineData("test.xml", "test.xml")] // When bad Url just a filename with extension
    [InlineData("test", "test")] // When bad Url just a filename without extension
    [InlineData("/test.xml", "test.xml")] // When short bad Url is same with the filename
    [InlineData("/test.xml?q=123", "test.xml")] // When short bad Url with query string is same with the filename
    [InlineData("/test.xml?q=1&x=100.0&y=testName", "test.xml")] // When bad short Url with query params is same with the filename
    [InlineData("test.xml?q=1&x=100.0&y=testName", "test.xml")] // When bad Url with query params is same with the filename
    [InlineData("http://www.a.com/test.xml?q=1&x=100.0&y=test", "test.xml")] // When complex Url with query params is same with the filename
    [InlineData("https://rs17.seedr.cc/get_zip_ngen_free/149605004/test.xml?st=XGSqYEtPiKmJcU-2PNNxjg&e=1663157407", "test.xml")] // When complex Url with query params is same with the filename

    public void GetFileNameFromUrlTest(string url, string expectedFilename)
    {
        // act
        var actualFilename = new Request(url).GetFileNameFromUrl();

        // assert
        Assert.Equal(expectedFilename, actualFilename);
    }






    [Fact]
    public void GetFileNameWithUrlAndQueryParamsAndFragmentIdentifierTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";

        // act
        var actualFilename = new Request(url).GetFileNameFromUrl();

        // assert
        Assert.Equal(filename, actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenNoUrlFileNameTest()
    {
        // arrange
        var url = DummyFileHelper.GetFileWithContentDispositionUrl(DummyFileHelper.SampleFile1KbName, DummyFileHelper.FileSize1Kb);

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Equal(DummyFileHelper.SampleFile1KbName, actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenNoUrlTest()
    {
        // arrange
        var url = "  ";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenBadUrlTest()
    {
        // arrange
        var url = "http://www.a.com/a/b/c/d/e/";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWhenBadUrlWithFilenameTest()
    {
        // arrange
        var filename = "test";
        var url = "http://www.a.com/a/b/c/" + filename;

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameTest()
    {
        // arrange
        var filename = "test.xml";
        var url = filename;

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameWithoutExtensionTest()
    {
        // arrange
        var filename = "test";
        var url = filename;

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithShortUrlTest()
    {
        // arrange
        var filename = "test.xml";
        var url = "/" + filename;

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithShortUrlAndQueryParamTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"/{filename}?q=1";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithShortUrlAndQueryParamsTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"/{filename}?q=1&x=100.0&y=testName";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithJustFilenameAndQueryParamsTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"{filename}?q=1&x=100.0&y=testName";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithUrlAndQueryParamsTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"http://www.a.com/{filename}?q=1&x=1&filename=test";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithUrlAndQueryParamsAndFragmentIdentifierTest()
    {
        // arrange
        var filename = "test.xml";
        var url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetUrlDispositionWithLongUrlTest()
    {
        // arrange
        var filename = "excel_sample.xls";
        var url = $"https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/{filename}?test=1";

        // act
        var actualFilename = await new Request(url).GetUrlDispositionFilenameAsync();

        // assert
        Assert.Null(actualFilename);
    }

    [Fact]
    public async Task GetFileNameOnRedirectUrlTest()
    {
        // arrange
        var filename = "test.zip";
        var url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);

        // act
        var actualFilename = await new Request(url).GetFileName();

        // assert
        Assert.Equal(filename, actualFilename);
    }

    [Fact]
    public void GetRedirectUrlByLocationTest()
    {
        // arrange
        var filename = "test.zip";
        var url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);
        var redirectUrl = DummyFileHelper.GetFileWithNameUrl(filename, DummyFileHelper.FileSize1Kb);
        var request = new Request(url);

        // act
        var resp = WebRequest.Create(url).GetResponse();
        resp.Headers.Add("Location", redirectUrl);
        var actualRedirectUrl = request.GetRedirectUrl(resp);

        // assert
        Assert.NotEqual(url, redirectUrl);
        Assert.NotEqual(request.Address.ToString(), redirectUrl);
        Assert.Equal(redirectUrl, actualRedirectUrl.AbsoluteUri);
    }

    [Fact]
    public void GetRedirectUrlWithoutLocationTest()
    {
        // arrange
        var filename = "test.zip";
        var url = DummyFileHelper.GetFileWithNameOnRedirectUrl(filename, DummyFileHelper.FileSize1Kb);
        var redirectUrl = DummyFileHelper.GetFileWithNameUrl(filename, DummyFileHelper.FileSize1Kb);
        var request = new Request(url);

        // act
        var resp = WebRequest.Create(url).GetResponse();
        var actualRedirectUrl = request.GetRedirectUrl(resp);

        // assert
        Assert.NotEqual(url, redirectUrl);
        Assert.NotEqual(request.Address.ToString(), redirectUrl);
        Assert.Equal(redirectUrl, actualRedirectUrl.AbsoluteUri);
    }

    [Fact]
    public async Task GetFileSizeTest()
    {
        // arrange
        var url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        var expectedSize = DummyFileHelper.FileSize16Kb;

        // act
        var actualSize = await new Request(url).GetFileSize();

        // assert
        Assert.Equal(expectedSize, actualSize);
    }

    [Fact]
    public async Task IsSupportDownloadInRangeTest()
    {
        // arrange
        var url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);

        // act
        var actualCan = await new Request(url).IsSupportDownloadInRange();

        // assert
        Assert.True(actualCan);
    }

    [Fact]
    public void GetTotalSizeFromContentLengthTest()
    {
        // arrange
        var length = 23432L;
        var headers = new Dictionary<string, string>() { { "Content-Length", length.ToString() } };
        var request = new Request("www.example.com");

        // act
        var actualLength = request.GetTotalSizeFromContentLength(headers);

        // assert
        Assert.Equal(length, actualLength);
    }

    [Fact]
    public void GetTotalSizeFromContentLengthWhenNoHeaderTest()
    {
        // arrange
        var length = -1;
        var headers = new Dictionary<string, string>();
        var request = new Request("www.example.com");

        // act
        var actualLength = request.GetTotalSizeFromContentLength(headers);

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
        var request = new Request("www.example.com");
        var headers = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(contentRange) == false)
            headers["Content-Range"] = contentRange;

        // act
        var actualLength = request.GetTotalSizeFromContentRange(headers);

        // assert
        Assert.Equal(expectedLength, actualLength);
    }

    [Fact]
    public void ToUnicodeFromEnglishToEnglishTest()
    {
        // arrange
        byte[] inputRawBytes = Encoding.UTF8.GetBytes(EnglishText);
        string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
        Request requestInstance = new Request("");

        // act 
        string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

        // assert
        Assert.Equal(EnglishText, decodedEnglishText);
    }

    [Fact]
    public void ToUnicodeFromPersianToPersianTest()
    {
        // arrange
        byte[] inputRawBytes = Encoding.UTF8.GetBytes(PersianText);
        string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
        Request requestInstance = new Request("");

        // act 
        string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

        // assert
        Assert.Equal(PersianText, decodedEnglishText);
    }

    [Fact]
    public void ToUnicodeFromAllToAllWithExtensionTest()
    {
        // arrange
        string inputRawText = EnglishText + PersianText + ".ext";
        byte[] inputRawBytes = Encoding.UTF8.GetBytes(inputRawText);
        string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
        Request requestInstance = new Request("");

        // act 
        string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

        // assert
        Assert.Equal(inputRawText, decodedEnglishText);
    }

    [Fact]
    public void GetRequestWithCredentialsTest()
    {
        // arrange
        var requestConfig = new RequestConfiguration() {
            Credentials = new NetworkCredential("username", "password")
        };
        var request = new Request("http://test.com", requestConfig);

        // act
        var httpRequest = request.GetRequest();

        // assert
        Assert.NotNull(httpRequest.Credentials);
    }

    [Fact]
    public void GetRequestWithNullCredentialsTest()
    {
        // arrange
        var requestConfig = new RequestConfiguration();
        var request = new Request("http://test.com", requestConfig);

        // act
        var httpRequest = request.GetRequest();

        // assert
        Assert.Null(httpRequest.Credentials);
    }
}