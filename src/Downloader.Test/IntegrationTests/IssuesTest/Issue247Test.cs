namespace Downloader.Test.IntegrationTests.IssuesTest;

/// <summary>
/// GitHub issue #247: some <see cref="DownloadConfiguration"/> properties are meant to change while a
/// download runs (the speed limit follows the user's setting live; <c>ActiveChunks</c> is rewritten
/// by every chunk thread). Those must be safe to write from one thread while download threads read
/// them, and every actual change must reach the <c>PropertyChanged</c> subscribers exactly once.
/// </summary>
[Collection("Sequential")]
public class Issue247Test(ITestOutputHelper output) : BaseTestClass(output)
{
    private const int Rounds = 5000;
    private static readonly int Writers = Math.Max(4, Environment.ProcessorCount);

    /// <summary>
    /// Behaviour check: a speed limit lifted from another thread reaches the running chunks. At 1 KB/s
    /// this file needs ~8 minutes, so it can only finish inside the test timeout if the lift took
    /// effect; no stopwatch, so a slow machine cannot make it fail. The memory-visibility part of the
    /// fix cannot be shown failing on x64 by a test; the notification tests below are the ones that
    /// fail on the old check-then-set setter.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task SpeedLimitChangedFromAnotherThreadTakesEffectDuringDownload()
    {
        // arrange
        const int size = 512 * 1024;
        string url = DummyFileHelper.GetFileWithNameUrl(Path.GetRandomFileName(), size);
        DownloadConfiguration config = new() {
            ChunkCount = 4,
            ParallelCount = 4,
            ParallelDownload = true,
            BufferBlockSize = 1024,
            MaximumBytesPerSecond = 1024
        };
        await using DownloadService downloader = new(config, LogFactory);
        TaskCompletionSource firstProgress = new(TaskCreationOptions.RunContinuationsAsynchronously);
        downloader.DownloadProgressChanged += (_, _) => firstProgress.TrySetResult();

        // act — lift the limit from a thread that is not one of the download's threads
        Task<Stream> download = downloader.DownloadFileTaskAsync(url);
        await firstProgress.Task;
        await Task.Run(() => config.MaximumBytesPerSecond = 0);
        await using Stream stream = await download;

        // assert
        Assert.Equal(DownloadStatus.Completed, downloader.Package.Status);
        Assert.True(DummyData.GenerateOrderedBytes(size).AreEqual(stream), "Downloaded bytes do not match");
    }

    [Fact]
    public void ConcurrentWritesOfMaximumBytesPerSecondRaiseOneChangePerChange()
    {
        AssertOneChangeNotificationPerChange(nameof(DownloadConfiguration.MaximumBytesPerSecond),
            (config, round) => config.MaximumBytesPerSecond = round + 1);
    }

    [Fact]
    public void ConcurrentWritesOfActiveChunksRaiseOneChangePerChange()
    {
        AssertOneChangeNotificationPerChange(nameof(DownloadConfiguration.ActiveChunks),
            (config, round) => config.ActiveChunks = round + 2);
    }

    [Fact]
    public void ConcurrentWritesOfBufferBlockSizeRaiseOneChangePerChange()
    {
        AssertOneChangeNotificationPerChange(nameof(DownloadConfiguration.BufferBlockSize),
            (config, round) => config.BufferBlockSize = round + 2);
    }

    [Fact]
    public void ConcurrentWritesOfEnableLiveStreamingRaiseOneChangePerChange()
    {
        AssertOneChangeNotificationPerChange(nameof(DownloadConfiguration.EnableLiveStreaming),
            (config, round) => config.EnableLiveStreaming = round % 2 == 0);
    }

    [Fact]
    public void LiveStreamingFlagRoundTripsAndSurvivesClone()
    {
        DownloadConfiguration config = new() { EnableLiveStreaming = true };

        Assert.True(config.EnableLiveStreaming);
        Assert.True(((DownloadConfiguration)config.Clone()).EnableLiveStreaming);

        config.EnableLiveStreaming = false;
        Assert.False(config.EnableLiveStreaming);
    }

    /// <summary>
    /// Each round, several threads released together write the SAME new value. Exactly one of them
    /// changed the property, so exactly one notification must be raised — and the value is the one
    /// written. A non-atomic check-then-set lets several writers all see the old value and all notify.
    /// </summary>
    private void AssertOneChangeNotificationPerChange(string property, Action<DownloadConfiguration, int> write)
    {
        DownloadConfiguration config = new();
        int notifications = 0;
        config.PropertyChanged += (_, e) => {
            if (e.PropertyName == property)
                Interlocked.Increment(ref notifications);
        };
        // The test thread joins both barriers, so it counts a round's notifications before the
        // writers may start the next one.
        using Barrier start = new(Writers + 1);
        using Barrier done = new(Writers + 1);
        Thread[] threads = Enumerable.Range(0, Writers).Select(_ => new Thread(() => {
            for (int r = 0; r < Rounds; r++)
            {
                start.SignalAndWait();
                write(config, r);
                done.SignalAndWait();
            }
        })).ToArray();
        foreach (Thread thread in threads)
            thread.Start();

        List<int> duplicated = [];
        for (int r = 0; r < Rounds; r++)
        {
            start.SignalAndWait();
            done.SignalAndWait();
            int raised = Interlocked.Exchange(ref notifications, 0);
            if (raised != 1)
                duplicated.Add(raised);
        }
        foreach (Thread thread in threads)
            thread.Join();

        Output.WriteLine($"{property}: {duplicated.Count} of {Rounds} rounds raised a wrong number of notifications");
        Assert.Empty(duplicated);
    }
}
