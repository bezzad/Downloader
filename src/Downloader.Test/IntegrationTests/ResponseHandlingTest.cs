using System.Collections.Concurrent;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Error responses are disposed, chunk requests keep the probe headers, and a relative redirect
/// <c>Location</c> is resolved against the current address.
/// </summary>
public class ResponseHandlingTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private sealed class TrackedContent() : ByteArrayContent([1, 2, 3])
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class ServiceUnavailableHandler : HttpMessageHandler
    {
        public TrackedContent Content { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = Content });
        }
    }

    private static ConcurrentDictionary<string, string> GetProbeHeaders(SocketClient client)
    {
        PropertyInfo property = typeof(SocketClient).GetProperty("ResponseHeaders", BindingFlags.Instance | BindingFlags.NonPublic);
        return (ConcurrentDictionary<string, string>)property!.GetValue(client);
    }

    [Fact]
    public async Task ErrorResponseIsDisposed()
    {
        // arrange
        ServiceUnavailableHandler handler = new();
        using SocketClient client = new(new DownloadConfiguration { CustomHttpMessageHandlerFactory = () => handler });

        // act
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SendRequestAsync(new HttpRequestMessage(HttpMethod.Get, "http://example.com/file")));

        // assert
        Assert.True(handler.Content.IsDisposed);
    }

    [Fact(Timeout = 30_000)]
    public async Task ChunkRequestDoesNotChangeTheProbeHeaders()
    {
        // arrange
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        using SocketClient client = new(new DownloadConfiguration());
        Request request = new(url);
        await client.GetFileSizeAsync(request, CancellationToken.None);
        string probeRange = GetProbeHeaders(client)[HttpHeaderNames.ContentRange];

        // act: a chunk request for another range
        HttpRequestMessage chunk = request.GetRequest();
        chunk.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(100, 199);
        using (HttpResponseMessage response = await client.SendRequestAsync(chunk))
            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);

        // assert
        Assert.Equal($"bytes 0-0/{DummyFileHelper.FileSize16Kb}", probeRange);
        Assert.Equal(probeRange, GetProbeHeaders(client)[HttpHeaderNames.ContentRange]);
    }

    [Fact(Timeout = 30_000)]
    public async Task RelativeRedirectIsFollowedByTheManualPath()
    {
        // arrange: the handler does not follow redirects, so the library must do it
        DownloadConfiguration config = new() {
            ChunkCount = 2,
            ParallelDownload = true,
            MinimumSizeOfChunking = 0,
            MaxTryAgainOnFailure = 0,
            CustomHttpMessageHandlerFactory = () => new SocketsHttpHandler { AllowAutoRedirect = false }
        };
        string url = DummyFileHelper.GetFileWithNameOnRedirectUrl("file.bin", DummyFileHelper.FileSize16Kb);
        await using DownloadService service = new(config);

        // act
        await using Stream stream = await service.DownloadFileTaskAsync(url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(stream));
    }

    [Fact]
    public void RelativeLocationIsResolvedAgainstTheRequestAddress()
    {
        // arrange
        using SocketClient client = new(new DownloadConfiguration());
        HttpResponseMessage response = new(HttpStatusCode.OK) {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "http://example.com/a/b/old.zip")
        };
        response.Headers.Location = new Uri("../new.zip", UriKind.Relative);

        // act
        Uri result = client.GetRedirectUrl(response);

        // assert
        Assert.Equal(new Uri("http://example.com/a/new.zip"), result);
    }
}
