namespace Downloader.Test.UnitTests;

/// <summary>
/// Tests for the internal <see cref="IDownload"/> implementation (Download.cs) built via
/// <see cref="DownloadBuilder"/>: event (un)subscription wiring, status/size delegation,
/// Stop, and synchronous/asynchronous disposal.
/// </summary>
public class DownloadTest : BaseTestClass
{
    private readonly string _url;

    public DownloadTest(ITestOutputHelper output) : base(output)
    {
        _url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
    }

    [Fact]
    public void StatusAndSizesReflectPackageBeforeStart()
    {
        // arrange
        IDownload download = DownloadBuilder.New().WithUrl(_url).Build();

        // act & assert — before any download runs the package is empty.
        Assert.Equal(DownloadStatus.None, download.Status);
        Assert.Equal(0, download.DownloadedFileSize);
        Assert.Equal(0, download.TotalFileSize);
    }

    [Fact]
    public void EventSubscribeAndUnsubscribeDoNotThrow()
    {
        // arrange
        IDownload download = DownloadBuilder.New().WithUrl(_url).Build();
        void OnChunk(object s, DownloadProgressChangedEventArgs e) { }
        void OnProgress(object s, DownloadProgressChangedEventArgs e) { }
        void OnCompleted(object s, AsyncCompletedEventArgs e) { }
        void OnStarted(object s, DownloadStartedEventArgs e) { }

        // act — exercise both the add and remove accessors of every event.
        download.ChunkDownloadProgressChanged += OnChunk;
        download.DownloadProgressChanged += OnProgress;
        download.DownloadFileCompleted += OnCompleted;
        download.DownloadStarted += OnStarted;

        download.ChunkDownloadProgressChanged -= OnChunk;
        download.DownloadProgressChanged -= OnProgress;
        download.DownloadFileCompleted -= OnCompleted;
        download.DownloadStarted -= OnStarted;

        // assert — no exception means the add/remove wiring forwarded to the service correctly.
        Assert.NotNull(download);
    }

    [Fact(Timeout = 60_000)]
    public async Task StatusAndSizesReflectCompletedDownload()
    {
        // arrange
        IDownload download = DownloadBuilder.New().WithUrl(_url).Build();
        bool started = false;
        download.DownloadStarted += (_, _) => started = true;

        // act
        await using Stream stream = await download.StartAsync();

        // assert — the download truly completed (stream holds the full payload) and the status
        // and size getters are reachable and consistent (non-negative) after completion.
        Assert.True(started);
        Assert.Equal(DownloadStatus.Completed, download.Status);
        Assert.Equal(DummyFileHelper.FileSize16Kb, stream.Length);
        Assert.True(download.DownloadedFileSize >= 0);
        Assert.True(download.TotalFileSize >= download.DownloadedFileSize);
    }

    [Fact(Timeout = 60_000)]
    public async Task StopCancelsRunningDownload()
    {
        // arrange — throttle so the download stays running long enough to be stopped mid-flight.
        IDownload download = DownloadBuilder.New()
            .WithUrl(_url)
            .Configure(c => c.MaximumBytesPerSecond = 1024)
            .Build();
        using ManualResetEventSlim running = new(false);
        download.DownloadProgressChanged += (_, _) => running.Set();

        // act — start without awaiting, then call the synchronous Stop() from the test thread
        // (never from the event callback thread, which would block the chunk worker).
        Task<Stream> downloadTask = download.StartAsync();
        running.Wait(TimeSpan.FromSeconds(30));
        download.Stop();
        await downloadTask;

        // assert — Stop() routes to CancelTaskAsync().Wait(); the download must not complete.
        Assert.NotEqual(DownloadStatus.Completed, download.Status);
    }

    [Fact(Timeout = 60_000)]
    public async Task DisposeAsyncClearsPackage()
    {
        // arrange
        IDownload download = DownloadBuilder.New().WithUrl(_url).Build();
        await download.StartAsync();

        // act
        await download.DisposeAsync();

        // assert
        Assert.Null(download.Package);
    }

    [Fact(Timeout = 30_000)]
    public async Task DisposeClearsPackage()
    {
        // arrange
        IDownload download = DownloadBuilder.New().WithUrl(_url).Build();

        // act — synchronous Dispose() path (Clear().Wait()); run off the test thread so the
        // xUnit timeout can interrupt a potential deadlock.
        await Task.Run(() => download.Dispose());

        // assert
        Assert.Null(download.Package);
    }
}
