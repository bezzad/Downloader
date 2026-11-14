namespace Downloader.Test.IntegrationTests;

/// <summary>
/// H1 of the 7.0.1 review: an error before the download really starts must not leave the service
/// waiting for ever. <see cref="AbstractDownloadService.Dispose"/> and the next download must end.
/// </summary>
public class SetupErrorTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(8);

    private static async Task<bool> EndsInTime(Func<Task> action)
    {
        Task task = Task.Run(action);
        bool ended = await Task.WhenAny(task, Task.Delay(Deadline)) == task;
        if (ended)
            await Task.WhenAny(task); // observe a possible exception without throwing it
        return ended;
    }

    private async Task AssertServiceIsUsable(DownloadService service)
    {
        Assert.NotEqual(DownloadStatus.Created, service.Status);
        Assert.True(await EndsInTime(() => service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(1024))),
            "a second download call on the service did not end");
        Assert.True(await EndsInTime(() => Task.Run(service.Dispose)), "Dispose() did not end");
    }

    [Fact]
    public async Task BadRequestHeaderDoesNotHangTheService()
    {
        // arrange: Content-Type is a content header, so HttpClient refuses it as a request header
        DownloadConfiguration config = new();
        config.RequestConfiguration.Headers.Add("Content-Type", "application/json");
        DownloadService service = new(config);

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(1024)));

        // assert
        await AssertServiceIsUsable(service);
    }

    [Fact]
    public async Task ThrowingClientFactoryDoesNotHangTheService()
    {
        // arrange
        DownloadService service = new(new DownloadConfiguration {
            CustomHttpClientFactory = () => throw new InvalidOperationException("no client (test)")
        });

        // act
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(1024)));

        // assert
        await AssertServiceIsUsable(service);
    }
}
