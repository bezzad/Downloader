using System.Collections.Concurrent;

namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// Integration tests for GitHub issue #256: with a parallel download, every chunk thread raised
/// <see cref="IDownloadService.DownloadProgressChanged"/> itself, so the handler ran on several threads at once
/// and an older total could arrive after a newer one. Now the storage writer reports the bytes it wrote, and one
/// deliverer raises the event, one call at a time, without making the download wait for the handler.
/// </summary>
[Collection("Sequential")]
public class Issue256Test(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Size = 1024 * 1024;
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    private static DownloadConfiguration ParallelConfig() => new() {
        ParallelDownload = true,
        ChunkCount = 8,
        ParallelCount = 8,
        BufferBlockSize = 1024
    };

    [Fact(Timeout = 60_000)]
    public async Task ProgressHandlerNeverRunsTwiceAtOnceAndNeverGoesDown()
    {
        // arrange
        await using DownloadService downloader = new(ParallelConfig(), LogFactory);
        int running = 0;
        int overlaps = 0;
        long lastReceived = 0;
        int wentDown = 0;
        TaskCompletionSource lastEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        downloader.DownloadProgressChanged += (_, e) => {
            if (Interlocked.Increment(ref running) > 1)
                Interlocked.Increment(ref overlaps);

            if (e.ReceivedBytesSize < lastReceived)
                Interlocked.Increment(ref wentDown);
            lastReceived = e.ReceivedBytesSize;
            Thread.Sleep(1); // give another thread time to come in, if it can
            Interlocked.Decrement(ref running);

            if (e.ReceivedBytesSize == Size)
                lastEvent.TrySetResult();
        };

        // act
        await using Stream result = await downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));
        await lastEvent.Task.WaitAsync(EventTimeout);

        // assert
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        Assert.Equal(0, overlaps);
        Assert.Equal(0, wentDown);
        Assert.Equal(100.0, downloader.Package.SaveProgress);
    }

    [Fact(Timeout = 60_000)]
    public async Task SlowProgressHandlerDoesNotSlowTheDownload()
    {
        // arrange — 1024 blocks: if the download waited 200 ms for each event, it would take minutes.
        await using DownloadService downloader = new(ParallelConfig(), LogFactory);
        long sumOfProgressedBytes = 0;
        TaskCompletionSource lastEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        downloader.DownloadProgressChanged += (_, e) => {
            Interlocked.Add(ref sumOfProgressedBytes, e.ProgressedByteSize);
            if (e.ReceivedBytesSize == Size)
                lastEvent.TrySetResult();
            else
                Thread.Sleep(200);
        };

        // act
        Stopwatch watch = Stopwatch.StartNew();
        await using Stream result = await downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));
        TimeSpan downloadTime = watch.Elapsed;
        await lastEvent.Task.WaitAsync(EventTimeout);

        // assert — the newest total still arrives, and no byte is lost or counted twice.
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        Assert.True(downloadTime < TimeSpan.FromSeconds(20), $"The download took {downloadTime}.");
        Assert.Equal(Size, Interlocked.Read(ref sumOfProgressedBytes));
    }

    [Fact(Timeout = 60_000)]
    public async Task ProgressHandlerThatThrowsDoesNotBreakTheDownload()
    {
        // arrange
        await using DownloadService downloader = new(ParallelConfig(), LogFactory);
        downloader.DownloadProgressChanged += (_, _) => throw new InvalidOperationException("handler bug");

        // act
        await using Stream result = await downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));

        // assert
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        Assert.Equal(Size, result.Length);
    }

    [Fact(Timeout = 60_000)]
    public async Task LiveStreamingGivesEveryByteInFileOrder()
    {
        // arrange — a parallel config: live streaming must still use one chunk, in file order.
        DownloadConfiguration config = ParallelConfig();
        config.EnableLiveStreaming = true;
        await using DownloadService downloader = new(config, LogFactory);
        MemoryStream streamed = new();
        int running = 0;
        int overlaps = 0;
        ConcurrentDictionary<string, bool> chunkIds = new();
        downloader.ChunkDownloadProgressChanged += (_, e) => chunkIds[e.ProgressId] = true;
        TaskCompletionSource lastEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        downloader.DownloadProgressChanged += (_, e) => {
            if (Interlocked.Increment(ref running) > 1)
                Interlocked.Increment(ref overlaps);

            streamed.Write(e.ReceivedBytes.Span);
            Interlocked.Decrement(ref running);
            if (e.ReceivedBytesSize == Size)
                lastEvent.TrySetResult();
        };

        // act
        await using Stream result = await downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));
        await lastEvent.Task.WaitAsync(EventTimeout);

        // assert
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        Assert.Equal(0, overlaps);
        Assert.Single(chunkIds);
        Assert.Equal(DummyData.GenerateOrderedBytes(Size), streamed.ToArray());
        Assert.True(config.ParallelDownload, "The caller's configuration must not change.");
    }

    [Fact(Timeout = 60_000)]
    public async Task LiveStreamingWaitsForASlowHandlerWithinTheMemoryLimit()
    {
        // arrange — the handler holds the first block until the test lets it go.
        const long limit = 16 * 1024;
        DownloadConfiguration config = new() {
            EnableLiveStreaming = true,
            BufferBlockSize = 1024,
            MaximumMemoryBufferBytes = limit
        };
        await using DownloadService downloader = new(config, LogFactory);
        using ManualResetEventSlim release = new(false);
        TaskCompletionSource firstEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource lastEvent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        long streamedBytes = 0;
        downloader.DownloadProgressChanged += (_, e) => {
            firstEvent.TrySetResult();
            release.Wait();
            streamedBytes += e.ReceivedBytes.Length;
            if (e.ReceivedBytesSize == Size)
                lastEvent.TrySetResult();
        };

        // act
        Task<Stream> download = downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));
        await firstEvent.Task.WaitAsync(EventTimeout);
        await Task.Delay(1000); // time to run ahead, if it could
        long receivedWhileHeld = downloader.Package.ReceivedBytesSize;
        release.Set();
        await using Stream result = await download;
        await lastEvent.Task.WaitAsync(EventTimeout);

        // assert — held back to the live limit plus the write buffer (same limit) and a few blocks in flight.
        Assert.True(receivedWhileHeld <= 3 * limit, $"Received {receivedWhileHeld} bytes while the handler was held.");
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        Assert.Equal(Size, streamedBytes);
    }

    [Fact(Timeout = 60_000)]
    public async Task EnableLiveStreamingCannotChangeWhileADownloadRuns()
    {
        // arrange
        DownloadConfiguration config = new() { BufferBlockSize = 1024 };
        await using DownloadService downloader = new(config, LogFactory);
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        downloader.DownloadStarted += (_, _) => {
            downloader.Pause();
            started.TrySetResult();
        };

        // act
        Task<Stream> download = downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(Size));
        await started.Task.WaitAsync(EventTimeout);

        // assert — a change throws, the same value does not, and a copy is free to change.
        Assert.Throws<InvalidOperationException>(() => config.EnableLiveStreaming = true);
        config.EnableLiveStreaming = false;
        DownloadConfiguration copy = (DownloadConfiguration)config.Clone();
        copy.EnableLiveStreaming = true;
        Assert.False(config.EnableLiveStreaming);

        downloader.Resume();
        await using Stream result = await download;
        Assert.Equal(DownloadStatus.Completed, downloader.Status);
        config.EnableLiveStreaming = true; // the download is over
        Assert.True(config.EnableLiveStreaming);
    }
}
