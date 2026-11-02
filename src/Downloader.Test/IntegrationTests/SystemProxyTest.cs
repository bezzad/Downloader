using Downloader.Test.Helper;
using System.Net;
using System.Net.Http;

namespace Downloader.Test.IntegrationTests;

// HttpClient.DefaultProxy is global, so these tests must not run beside other tests.
[CollectionDefinition(nameof(SystemProxyTest), DisableParallelization = true)]
public class SystemProxyCollection;

[Collection(nameof(SystemProxyTest))]
public class SystemProxyTest(ITestOutputHelper output) : BaseTestClass(output)
{
    // This host does not exist, so the download can only work through the proxy.
    private const string Url = "http://downloader.invalid/file.bin";

    private static DownloadConfiguration NewConfig() => new() { MaxTryAgainOnFailure = 1 };

    private static async Task<(DownloadService, byte[])> Download(DownloadConfiguration config)
    {
        DownloadService service = new(config);
        await using Stream result = await service.DownloadFileTaskAsync(Url);
        using MemoryStream copy = new();
        if (result != null)
            await result.CopyToAsync(copy);
        return (service, copy.ToArray());
    }

    [Fact(Timeout = 60_000)]
    public async Task SystemProxyIsUsedWhenNoProxyIsSet()
    {
        // arrange
        using FakeProxyServer proxy = new(DummyData.GenerateOrderedBytes(1024));
        IWebProxy old = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = new WebProxy(proxy.Url);
        try
        {
            // act
            (DownloadService service, byte[] data) = await Download(NewConfig());
            await using DownloadService _ = service;

            // assert
            Assert.Equal(DownloadStatus.Completed, service.Status);
            Assert.Equal(proxy.Body, data);
            Assert.True(proxy.Requests > 0);
        }
        finally
        {
            HttpClient.DefaultProxy = old;
        }
    }

    [Fact(Timeout = 60_000)]
    public async Task ConfiguredProxyWinsOverTheSystemProxy()
    {
        // arrange
        using FakeProxyServer system = new(DummyData.GenerateOrderedBytes(1024));
        using FakeProxyServer own = new(DummyData.GenerateSingleBytes(1024, 7));
        IWebProxy old = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = new WebProxy(system.Url);
        DownloadConfiguration config = NewConfig();
        config.RequestConfiguration.Proxy = new WebProxy(own.Url);
        try
        {
            // act
            (DownloadService service, byte[] data) = await Download(config);
            await using DownloadService _ = service;

            // assert
            Assert.Equal(DownloadStatus.Completed, service.Status);
            Assert.Equal(own.Body, data);
            Assert.Equal(0, system.Requests);
        }
        finally
        {
            HttpClient.DefaultProxy = old;
        }
    }
}
