using Downloader.Extensions;

namespace Downloader.Test.UnitTests;

/// <summary>
/// Small edge cases of internal helpers that the bigger test classes do not reach.
/// </summary>
public class EdgeCaseTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void EnsurePathEncodedKeepsSurrogatePairsTogether()
    {
        // act
        string result = UrlHelper.EnsurePathEncoded("https://example.com/a😀.txt");

        // assert
        Assert.Equal("https://example.com/a%F0%9F%98%80.txt", result);
    }

    [Fact]
    public void TrySplitChunkRejectsNullChunkAndZeroMinimumSize()
    {
        // arrange
        Chunk chunk = new(0, 999);
        DownloadPackage package = new() { Chunks = [chunk] };

        // act & assert
        Assert.False(package.TrySplitChunk(null, 10, out Chunk tail1));
        Assert.False(package.TrySplitChunk(chunk, 0, out Chunk tail2));
        Assert.Null(tail1);
        Assert.Null(tail2);
    }

    [Fact]
    public void TrySplitChunkRejectsUnknownChunk()
    {
        // arrange
        DownloadPackage package = new() { Chunks = null };

        // act & assert
        Assert.False(package.TrySplitChunk(new Chunk(0, 999), 10, out _));
    }

    [Theory]
    [InlineData(DownloadStatus.Completed)]
    [InlineData(DownloadStatus.Failed)]
    public async Task FinishedStateCannotBeSetAgain(DownloadStatus finished)
    {
        // arrange
        DownloadPackage package = new();
        package.SetState(finished);

        // act
        bool changed = await package.TrySetCompleteState(DownloadStatus.Stopped);

        // assert
        Assert.False(changed);
        Assert.Equal(finished, package.Status);
    }

    [Fact]
    public async Task CompletedFileReplacesAnOldFileWithTheSameName()
    {
        // arrange
        string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(fileName, "old");
        DownloadPackage package = new() {
            FileName = fileName,
            DownloadingFileExtension = ".download",
            TotalFileSize = 3
        };
        package.BuildStorage(0);
        await package.Storage.WriteAsync(0, "new"u8.ToArray(), 3, false);
        package.SetState(DownloadStatus.Running);

        try
        {
            // act
            bool changed = await package.TrySetCompleteState(DownloadStatus.Completed);

            // assert
            Assert.True(changed);
            Assert.Equal("new", await File.ReadAllTextAsync(fileName));
            Assert.False(File.Exists(package.DownloadingFileName));
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    [Fact]
    public void MinimumChunkSizeBiggerThanFileGivesOneChunk()
    {
        // arrange
        DownloadConfiguration config = new() { ChunkCount = 8, MinimumChunkSize = 1000 };
        DownloadPackage package = new() { TotalFileSize = 100 };

        // act
        new ChunkHub(config).SetFileChunks(package);

        // assert
        Assert.Single(package.Chunks);
    }

    [Fact]
    public async Task SchedulerIgnoresCompleteOfUnknownChunk()
    {
        // arrange
        DownloadPackage package = new() { Chunks = [new Chunk(0, 9)] };
        DynamicChunkScheduler scheduler = new(package, 0);

        // act
        scheduler.Complete(new Chunk(10, 19)); // never handed out

        // assert: the queued chunk is still given out
        Assert.Same(package.Chunks[0], await scheduler.GetNextAsync(CancellationToken.None));
    }

    [Fact]
    public void SchedulerNeedsAPackage()
    {
        Assert.Throws<ArgumentNullException>(() => new DynamicChunkScheduler(null, 0));
    }

    [Fact]
    public async Task RemoteFileResolverThrowsWhenCallerCancels()
    {
        // arrange
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();
        string url = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb);

        // act & assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            RemoteFileResolver.GetFileInfoAsync(url, null, cts.Token));
    }

    [Fact]
    public void DownloadStartedEventArgsKeepsValues()
    {
        // act
        DownloadStartedEventArgs args = new("file.zip", 123);

        // assert
        Assert.Equal("file.zip", args.FileName);
        Assert.Equal(123, args.TotalBytesToReceive);
    }

    [Fact]
    public async Task ResumeOfAPauseTokenSourceThatIsNotPausedDoesNothing()
    {
        // arrange
        PauseTokenSource source = new();

        // act
        source.Resume();
        source.Pause();
        source.Pause(); // second pause keeps the same wait
        Task wait = source.WaitWhilePausedAsync();
        source.Resume();
        await wait.WaitAsync(TimeSpan.FromSeconds(5));

        // assert
        Assert.False(source.IsPaused);
    }
}
