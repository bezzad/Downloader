using System.Net.Http.Headers;

namespace Downloader.Test.UnitTests;

public class RequestTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private const string EnglishText = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string PersianText = "۰۱۲۳۴۵۶۷۸۹ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیًٌٍَُِّْؤئيإأآة»«:؛كٰ‌ٔء؟";
    private static readonly Encoding Latin1Encoding = Encoding.GetEncoding("iso-8859-1");

    [Theory]
    [InlineData("    ", "")] // When No Url
    [InlineData("http://www.a.com/a/b/c/d/e/", "")] // When Bad Url
    [InlineData("http://www.a.com/a/b/c/filename", "filename")] // When bad Url with filename
    [InlineData("test.xml", "test.xml")] // When bad Url just a filename with extension
    [InlineData("test", "test")] // When bad Url just a filename without extension
    [InlineData("/test.xml", "test.xml")] // When short bad Url is same with the filename
    [InlineData("/test.xml?q=123", "test.xml")] // When short bad Url with query string is same with the filename
    [InlineData("/test.xml?q=1&x=100.0&y=testName",
        "test.xml")] // When bad short Url with query params is same with the filename
    [InlineData("test.xml?q=1&x=100.0&y=testName",
        "test.xml")] // When bad Url with query params is same with the filename
    [InlineData("http://www.a.com/test.xml?q=1&x=100.0&y=test",
        "test.xml")] // When complex Url with query params is same with the filename
    [InlineData("https://rs17.seedr.cc/get_zip_ngen_free/149605004/test.xml?st=XGSqYEtPiKmJcU-2PNNxjg&e=1663157407",
        "test.xml")] // When complex Url with query params is same with the filename
    public void GetFileNameFromUrlTest(string url, string expectedFilename)
    {
        // act
        string actualFilename = new Request(url).GetFileNameFromUrl();

        // assert
        Assert.Equal(expectedFilename, actualFilename);
    }

    [Fact]
    public void GetFileNameWithUrlAndQueryParamsAndFragmentIdentifierTest()
    {
        // arrange
        string filename = "test.xml";
        string url = $"http://www.a.com/{filename}?q=1&x=3#aidjsf";

        // act
        string actualFilename = new Request(url).GetFileNameFromUrl();

        // assert
        Assert.Equal(filename, actualFilename);
    }

    [Fact]
    public void ToUnicodeFromEnglishToEnglishTest()
    {
        // arrange
        byte[] inputRawBytes = Encoding.UTF8.GetBytes(EnglishText);
        string inputEncodedText = Latin1Encoding.GetString(inputRawBytes);
        Request requestInstance = new("");

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
        Request requestInstance = new("");

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
        Request requestInstance = new("");

        // act 
        string decodedEnglishText = requestInstance.ToUnicode(inputEncodedText);

        // assert
        Assert.Equal(inputRawText, decodedEnglishText);
    }

    [Fact]
    public void GetRequestWithCredentialsTest()
    {
        // arrange
        RequestConfiguration requestConfig = new() {
            Credentials = new NetworkCredential("username", "password"),
            PreAuthenticate = true // Optional: Pre-authenticate to send credentials upfront
        };
        Request request = new("https://google.com", requestConfig);

        // act
        HttpRequestMessage httpRequest = request.GetRequest();

        // assert
        // read request credentials
        AuthenticationHeaderValue credentials = httpRequest.Headers.Authorization;
        Assert.NotNull(credentials);
    }

    [Fact]
    public void GetRequestWithNullCredentialsTest()
    {
        // arrange
        RequestConfiguration requestConfig = new();
        Request request = new("http://test.com", requestConfig);

        // act
        HttpRequestMessage httpRequest = request.GetRequest();

        // assert
        AuthenticationHeaderValue credentials = httpRequest.Headers.Authorization;
        Assert.Null(credentials);
        // Assert.Null(httpRequest.Credentials);
    }
}