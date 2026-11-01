using System.Net.Http.Headers;
using Downloader.DummyHttpServer.Controllers;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// Credentials belong to the host the caller asked for; a redirect to another host must not get them.
/// </summary>
public class AuthorizationRedirectTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static DownloadConfiguration NewConfig(RequestConfiguration request) => new() {
        ChunkCount = 4,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        RequestConfiguration = request
    };

    private static async Task<(int WithAuth, int WithoutAuth)> Download(string host, RequestConfiguration request)
    {
        string id = Guid.NewGuid().ToString("N");
        string url = DummyFileHelper.GetRedirectToHostUrl(id, DummyFileHelper.FileSize16Kb, host);
        await using DownloadService service = new(NewConfig(request));

        await using Stream result = await service.DownloadFileTaskAsync(url);

        Assert.Equal(DownloadStatus.Completed, service.Status);
        Assert.True(DummyFileHelper.File16Kb.AreEqual(result));
        return DummyFileController.AuthorizationCounts[id];
    }

    [Fact(Timeout = 60_000)]
    public async Task AuthorizationIsNotSentToAnotherHost()
    {
        // act
        var counts = await Download("127.0.0.1", new RequestConfiguration {
            Authorization = new AuthenticationHeaderValue("Bearer", "secret")
        });

        // assert: only the first request (to localhost) had the header
        Assert.Equal(1, counts.WithAuth);
        Assert.True(counts.WithoutAuth >= 4);
    }

    [Fact(Timeout = 60_000)]
    public async Task CredentialsAreNotSentToAnotherHost()
    {
        // act
        var counts = await Download("127.0.0.1", new RequestConfiguration {
            Credentials = new NetworkCredential("user", "password")
        });

        // assert
        Assert.Equal(1, counts.WithAuth);
        Assert.True(counts.WithoutAuth >= 4);
    }

    [Fact(Timeout = 60_000)]
    public async Task AuthorizationIsSentAfterSameHostRedirect()
    {
        // act
        var counts = await Download("localhost", new RequestConfiguration {
            Authorization = new AuthenticationHeaderValue("Bearer", "secret")
        });

        // assert: the chunk requests carry the header (.NET itself drops it on the followed redirect)
        Assert.True(counts.WithAuth >= 4);
    }

    [Fact]
    public void RequestKeepsAuthorizationOnlyForTheOriginalHost()
    {
        // arrange
        Request request = new("https://example.com/file", new RequestConfiguration {
            Authorization = new AuthenticationHeaderValue("Bearer", "secret")
        });

        // act
        bool sameHost = request.GetRequest().Headers.Authorization is not null;
        request.Address = new Uri("http://example.com/file"); // https -> http
        bool downgrade = request.GetRequest().Headers.Authorization is not null;
        request.Address = new Uri("https://example.com:8443/file"); // other port
        bool otherPort = request.GetRequest().Headers.Authorization is not null;
        request.Address = new Uri("https://EXAMPLE.com/other"); // same host, other path
        bool samePath = request.GetRequest().Headers.Authorization is not null;

        // assert
        Assert.True(sameHost);
        Assert.False(downgrade);
        Assert.False(otherPort);
        Assert.True(samePath);
    }
}
