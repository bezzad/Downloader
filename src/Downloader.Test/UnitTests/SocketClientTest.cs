namespace Downloader.Test.UnitTests;

public class SocketClientTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly SocketClient _socketClient = new(new DownloadConfiguration());

    [Fact]
    public async Task GetUrlDispositionWhenNoUrlFileNameTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileWithContentDispositionUrl(DummyFileHelper.SampleFile1KbName,
            DummyFileHelper.FileSize1Kb);
        Request request = new(url);

        // act
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        string actualFilename = await _socketClient.GetUrlDispositionFilenameAsync(request, CancellationToken.None);

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
        await _socketClient.SetRequestFileNameAsync(request, CancellationToken.None);

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
        HttpResponseMessage resp = await _socketClient.SendRequestAsync(msg, CancellationToken.None);
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
        long actualSize = await _socketClient.GetFileSizeAsync(request, CancellationToken.None);

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
        bool actualCan = await _socketClient.IsSupportDownloadInRange(request, CancellationToken.None);

        // assert
        Assert.True(actualCan);
    }

    [Fact]
    public async Task ThrowIfIsNotSupportDownloadInRangeDoesNotThrowWhenSupportedTest()
    {
        // arrange
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        Request request = new(url);

        // act & assert: a range-capable server must not throw
        await _socketClient.ThrowIfIsNotSupportDownloadInRange(request, CancellationToken.None);
    }

    [Fact]
    public async Task ThrowIfIsNotSupportDownloadInRangeThrowsWhenNotSupportedTest()
    {
        // arrange: server advertises no range support
        string url = DummyFileHelper.GetFileWithNoAcceptRangeUrl(DummyFileHelper.SampleFile16KbName,
            DummyFileHelper.FileSize16Kb);
        Request request = new(url);

        // act & assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _socketClient.ThrowIfIsNotSupportDownloadInRange(request, CancellationToken.None).AsTask());
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

    [Fact]
    public void HttpClientUsesFallbackUserAgentWhenConfigurationUserAgentIsNullOrEmpty()
    {
        foreach (string userAgent in new string[] { null, string.Empty, "Downloader/" })
        {
            // arrange
            RequestConfiguration requestConfiguration = new() {
                UserAgent = userAgent
            };
            DownloadConfiguration downloadConfiguration = new() {
                RequestConfiguration = requestConfiguration
            };

            // act
            using SocketClient socketClient = new(downloadConfiguration);
            string actualUserAgent = GetClient(socketClient).DefaultRequestHeaders.UserAgent.ToString();

            // assert
            Assert.False(string.IsNullOrWhiteSpace(actualUserAgent));
            Assert.StartsWith("Downloader/", actualUserAgent);
            Assert.False(actualUserAgent.EndsWith('/'));
        }
    }

    [Fact]
    public void HttpClientKeepsCustomUserAgent()
    {
        // arrange
        const string expectedUserAgent = "CustomDownloader/1.2.3";
        RequestConfiguration requestConfiguration = new() {
            UserAgent = expectedUserAgent
        };
        DownloadConfiguration downloadConfiguration = new() {
            RequestConfiguration = requestConfiguration
        };

        // act
        using SocketClient socketClient = new(downloadConfiguration);
        string actualUserAgent = GetClient(socketClient).DefaultRequestHeaders.UserAgent.ToString();

        // assert
        Assert.Equal(expectedUserAgent, actualUserAgent);
    }

    [Fact]
    public void HttpClientSetsDefaultAcceptHeaderWhenAcceptIsNotConfigured()
    {
        // arrange
        RequestConfiguration requestConfiguration = new() {
            Accept = null
        };
        DownloadConfiguration downloadConfiguration = new() {
            RequestConfiguration = requestConfiguration
        };

        // act
        using SocketClient socketClient = new(downloadConfiguration);
        string actualAccept = string.Join(",", GetClient(socketClient).DefaultRequestHeaders.Accept.Select(x => x.MediaType));

        // assert
        Assert.Equal("*/*", actualAccept);
    }

    [Fact]
    public void HttpClientKeepsCustomAcceptHeader()
    {
        // arrange
        const string expectedAccept = "application/json";
        RequestConfiguration requestConfiguration = new() {
            Accept = expectedAccept
        };
        DownloadConfiguration downloadConfiguration = new() {
            RequestConfiguration = requestConfiguration
        };

        // act
        using SocketClient socketClient = new(downloadConfiguration);
        string actualAccept = string.Join(",", GetClient(socketClient).DefaultRequestHeaders.Accept.Select(x => x.MediaType));

        // assert
        Assert.Equal(expectedAccept, actualAccept);
    }

    [Fact]
    public void HandlerUsesRequestConfiguration()
    {
        // arrange
        var proxy = new WebProxy("http://127.0.0.1:1");
        var credentials = new NetworkCredential("user", "pass");
        using var certificate = CreateSelfSignedCertificate();
        var certificates = new System.Security.Cryptography.X509Certificates.X509CertificateCollection { certificate };
        RequestConfiguration config = new() {
            Proxy = proxy,
            Credentials = credentials,
            PreAuthenticate = true,
            ClientCertificates = certificates,
            KeepAlive = true,
            KeepAliveTimeout = TimeSpan.FromSeconds(7),
            Expect = "100-continue"
        };

        // act
        using SocketsHttpHandler handler = GetHandler(config);

        // assert
        Assert.True(handler.UseProxy);
        Assert.Same(proxy, handler.Proxy);
        Assert.Same(credentials, handler.Credentials);
        Assert.True(handler.PreAuthenticate);
        Assert.Same(certificates, handler.SslOptions.ClientCertificates);
        Assert.Equal(TimeSpan.FromSeconds(7), handler.KeepAlivePingTimeout);
        Assert.Equal(HttpKeepAlivePingPolicy.WithActiveRequests, handler.KeepAlivePingPolicy);
        Assert.Equal(TimeSpan.FromSeconds(1), handler.Expect100ContinueTimeout);
    }

    [Fact]
    public void HandlerWithoutProxyCookiesOrKeepAlive()
    {
        // arrange
        RequestConfiguration config = new() {
            Proxy = null,
            CookieContainer = null,
            KeepAlive = false
        };

        // act
        using SocketsHttpHandler handler = GetHandler(config);

        // assert: without a proxy the system proxy is used
        Assert.True(handler.UseProxy);
        Assert.Null(handler.Proxy);
        Assert.False(handler.UseCookies);
        Assert.Null(handler.Credentials);
        Assert.Null(handler.SslOptions.ClientCertificates);
    }

    [Fact]
    public void HttpClientAddsOptionalHeaders()
    {
        // arrange
        RequestConfiguration requestConfiguration = new() {
            Referer = "http://example.com/page",
            ContentType = "application/zip",
            TransferEncoding = "gzip",
            Expect = "100-continue",
            KeepAlive = false,
            Headers = new WebHeaderCollection { { "X-Test", "yes" }, { "X-Empty", "" } }
        };
        DownloadConfiguration downloadConfiguration = new() { RequestConfiguration = requestConfiguration };

        // act
        using SocketClient socketClient = new(downloadConfiguration);
        var headers = GetClient(socketClient).DefaultRequestHeaders;

        // assert
        Assert.Equal(new Uri("http://example.com/page"), headers.Referrer);
        Assert.Contains(headers.Accept, a => a.MediaType == "application/zip");
        Assert.Contains(headers.AcceptEncoding, e => e.Value == "gzip");
        Assert.Contains(headers.TransferEncoding, e => e.Value == "gzip");
        Assert.Contains(headers.Expect, e => e.Name == "100-continue");
        Assert.Equal("yes", headers.GetValues("X-Test").Single());
        Assert.False(headers.Contains("X-Empty"));
        Assert.Contains("close", headers.Connection);
    }

    [Fact]
    public async Task GzipResponseHasUnknownSizeAndNoRangeSupport()
    {
        // arrange
        string url = DummyFileHelper.GetGzipCompressedFileUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
        using SocketClient client = new(new DownloadConfiguration());
        Request request = new(url);

        // act
        bool canRange = await client.IsSupportDownloadInRange(request, CancellationToken.None);
        long size = await client.GetFileSizeAsync(request, CancellationToken.None);

        // assert
        Assert.False(canRange);
        Assert.Equal(-1, size);
    }

    private static SocketsHttpHandler GetHandler(RequestConfiguration config)
    {
        using SocketClient socketClient = new(new DownloadConfiguration());
        MethodInfo method = typeof(SocketClient).GetMethod("GetSocketsHttpHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return (SocketsHttpHandler)method!.Invoke(socketClient, [config]);
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=test", rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        return req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(1));
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

    private static HttpClient GetClient(SocketClient socketClient)
    {
        var propertyInfo = typeof(SocketClient).GetProperty("Client",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return (HttpClient)propertyInfo.GetValue(socketClient);
    }
}
