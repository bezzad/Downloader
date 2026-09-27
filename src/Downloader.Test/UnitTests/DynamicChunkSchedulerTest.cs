namespace Downloader.Test.UnitTests;

public class DynamicChunkSchedulerTest
{
    [Fact]
    public async Task IdleWorkerSplitsLargestTailOnlyAfterQueuedChunksAreTaken()
    {
        DownloadPackage package = Package(
            new Chunk(0, 99) { Id = "first" },
            new Chunk(100, 199) { Id = "second" });
        DynamicChunkScheduler scheduler = new(package, minimumChunkSize: 10);

        Chunk first = await scheduler.GetNextAsync(CancellationToken.None);
        Chunk second = await scheduler.GetNextAsync(CancellationToken.None);
        Task<Chunk> idleWorker = scheduler.GetNextAsync(CancellationToken.None).AsTask();

        first.Position = 20;
        Assert.False(scheduler.TrySplitAfterProgress(first));
        Assert.True(scheduler.TrySplitAfterProgress(second));

        Chunk stolenTail = await idleWorker;
        Assert.Equal("second", second.Id);
        Assert.Equal(100, second.Start);
        Assert.Equal(149, second.End);
        Assert.Equal(150, stolenTail.Start);
        Assert.Equal(199, stolenTail.End);
        AssertExactCoverage(package.Chunks, 0, 199);
    }

    [Fact]
    public async Task MoreIdleWorkersCanStealRepeatedlyWithoutGapsOrOverlaps()
    {
        DownloadPackage package = Package(
            new Chunk(0, 127) { Id = "slow" },
            new Chunk(128, 191) { Id = "fast-1" },
            new Chunk(192, 255) { Id = "fast-2" });
        DynamicChunkScheduler scheduler = new(package, minimumChunkSize: 16);

        Chunk slow = await scheduler.GetNextAsync(CancellationToken.None);
        Chunk fast1 = await scheduler.GetNextAsync(CancellationToken.None);
        Chunk fast2 = await scheduler.GetNextAsync(CancellationToken.None);
        scheduler.Complete(fast1);
        scheduler.Complete(fast2);

        Task<Chunk> firstIdleWorker = scheduler.GetNextAsync(CancellationToken.None).AsTask();
        Task<Chunk> secondIdleWorker = scheduler.GetNextAsync(CancellationToken.None).AsTask();

        Assert.True(scheduler.TrySplitAfterProgress(slow));
        Chunk firstTail = await firstIdleWorker;
        firstTail.Position = 48;
        Assert.True(scheduler.TrySplitAfterProgress(slow));
        Chunk secondTail = await secondIdleWorker;

        Assert.Equal(0, slow.Start);
        Assert.Equal(31, slow.End);
        Assert.Equal(32, secondTail.Start);
        Assert.Equal(63, secondTail.End);
        Assert.Equal(64, firstTail.Start);
        Assert.Equal(127, firstTail.End);
        AssertExactCoverage(package.Chunks, 0, 255);
    }

    [Fact]
    public async Task TailBelowTwoMinimumChunksIsNotSplit()
    {
        DownloadPackage package = Package(new Chunk(0, 31) { Position = 1 });
        DynamicChunkScheduler scheduler = new(package, minimumChunkSize: 16);
        Chunk active = await scheduler.GetNextAsync(CancellationToken.None);
        using CancellationTokenSource cancellation = new();
        Task<Chunk> idleWorker = scheduler.GetNextAsync(cancellation.Token).AsTask();

        Assert.False(scheduler.TrySplitAfterProgress(active));
        Assert.Single(package.Chunks);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => idleWorker);
    }

    [Fact]
    public async Task LastCompletionReleasesIdleWorkersWithoutInventingWork()
    {
        DownloadPackage package = Package(new Chunk(0, 31));
        DynamicChunkScheduler scheduler = new(package, minimumChunkSize: 0);
        Chunk active = await scheduler.GetNextAsync(CancellationToken.None);
        Task<Chunk> idleWorker = scheduler.GetNextAsync(CancellationToken.None).AsTask();

        scheduler.Complete(active);

        Assert.Null(await idleWorker);
    }

    private static DownloadPackage Package(params Chunk[] chunks)
    {
        return new DownloadPackage {
            TotalFileSize = chunks.Sum(chunk => chunk.Length),
            Chunks = chunks
        };
    }

    private static void AssertExactCoverage(IEnumerable<Chunk> chunks, long expectedStart, long expectedEnd)
    {
        Chunk[] ordered = chunks.OrderBy(chunk => chunk.Start).ToArray();
        Assert.Equal(expectedStart, ordered[0].Start);
        Assert.Equal(expectedEnd, ordered[^1].End);
        for (int i = 1; i < ordered.Length; i++)
            Assert.Equal(ordered[i - 1].End + 1, ordered[i].Start);
    }
}