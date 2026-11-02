using Downloader.Test.Helper;
using System.Net.Http;

namespace Downloader.Test.IntegrationTests;

public class TlsValidationTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static DownloadConfiguration NewConfig() => new(); // default retries: a TLS error must not be retried for ever

    [Fact(Timeout = 60_000)]
    public async Task SelfSignedCertificateFailsByDefault()
    {
        // arrange
        using SelfSignedHttpsServer server = new(DummyData.GenerateOrderedBytes(1024));
        await using DownloadService service = new(NewConfig());
        AsyncCompletedEventArgs completed = null;
        service.DownloadFileCompleted += (_, e) => completed = e;

        // act
        await using Stream result = await service.DownloadFileTaskAsync(server.Url);

        // assert
        Assert.Equal(DownloadStatus.Failed, service.Status);
        Assert.NotNull(completed?.Error);
    }

    [Fact(Timeout = 60_000)]
    public async Task SelfSignedCertificateWorksWithTheCallersOwnHandler()
    {
        // arrange
        using SelfSignedHttpsServer server = new(DummyData.GenerateOrderedBytes(1024));
        DownloadConfiguration config = NewConfig();
        config.CustomHttpMessageHandlerFactory = () => new SocketsHttpHandler {
            SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
        };
        await using DownloadService service = new(config);

        // act
        await using Stream result = await service.DownloadFileTaskAsync(server.Url);

        // assert
        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.True(server.Body.AreEqual(result));
    }
}
