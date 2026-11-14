using Downloader.Exceptions;

namespace Downloader.Test.IntegrationTests;

/// <summary>
/// C1 and C2 of the 7.0.1 review: a storage error must end the download, and the end of every
/// download must be reported exactly once with the real status.
/// </summary>
public class StorageErrorTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    /// <summary>A storage that fails like a full disk after <paramref name="failAfter"/> bytes.</summary>
    private sealed class FullDiskStream(long failAfter, int delayMs = 0) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrowIfFull(count);
            base.Write(buffer, offset, count);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (delayMs > 0)
                await Task.Delay(delayMs, cancellationToken);
            ThrowIfFull(buffer.Length);
            await base.WriteAsync(buffer, cancellationToken);
        }

        private void ThrowIfFull(int count)
        {
            if (Length + count > failAfter)
                throw new IOException("No space left on device (test)");
        }
    }

    private sealed record Result(bool Ended, DownloadStatus Status, int Events, AsyncCompletedEventArgs Args, Exception Thrown);

    private static async Task<Result> Run(DownloadService service, Func<Task> start)
    {
        int events = 0;
        AsyncCompletedEventArgs args = null;
        service.DownloadFileCompleted += (_, e) => {
            Interlocked.Increment(ref events);
            args = e;
        };

        Task run = Task.Run(start);
        bool ended = await Task.WhenAny(run, Task.Delay(Deadline)) == run;
        if (!ended)
            service.CancelAsync(); // do not leave a download running after the test

        return new Result(ended, service.Status, events, args, run.IsFaulted ? run.Exception?.GetBaseException() : null);
    }

    private static DownloadConfiguration NewConfig(int maxTry = int.MaxValue, long memoryLimit = 0) => new() {
        ChunkCount = 2,
        ParallelDownload = true,
        MinimumSizeOfChunking = 0,
        MaxTryAgainOnFailure = maxTry,
        BlockTimeout = 500,
        MaximumBytesPerSecond = 64 * 1024,
        MaximumMemoryBufferBytes = memoryLimit
    };

    [Fact]
    public async Task DiskErrorFailsTheDownloadAtOnce()
    {
        // arrange: default retries (int.MaxValue), the storage fails after 4 KB
        DownloadService service = new(NewConfig());
        DownloadPackage package = new() {
            Storage = new ConcurrentStream(new FullDiskStream(4096), 0),
            FileName = GetTempNoFilename()
        };

        // act
        Result result = await Run(service, () => service.DownloadFileTaskAsync(package, DummyFileHelper.GetFileUrl(256 * 1024)));

        // assert
        Assert.True(result.Ended, "the download did not end after a disk error");
        Assert.Null(result.Thrown);
        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(1, result.Events);
        Assert.IsType<StorageException>(result.Args.Error);
        Assert.False(result.Args.Cancelled);
    }

    [Fact]
    public async Task DiskErrorWithFullMemoryBufferFailsTheDownload()
    {
        // arrange: a slow storage and a small memory limit, so chunks wait for room when the writer dies
        DownloadService service = new(NewConfig(memoryLimit: 2048));
        DownloadPackage package = new() {
            Storage = new ConcurrentStream(new FullDiskStream(4096, delayMs: 300), 2048),
            FileName = GetTempNoFilename()
        };

        // act
        Result result = await Run(service, () => service.DownloadFileTaskAsync(package, DummyFileHelper.GetFileUrl(256 * 1024)));

        // assert
        Assert.True(result.Ended, "the download did not end after a disk error with a full memory buffer");
        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(1, result.Events);
        Assert.IsType<StorageException>(result.Args.Error);
    }

    [Theory]
    [InlineData(false)] // memory download: it was reported Completed, with no event
    [InlineData(true)]  // file download: Failed, but no event and the call threw
    public async Task StorageErrorIsReportedOnce(bool asFile)
    {
        // arrange: few retries, so the end of the download is reached
        DownloadService service = new(NewConfig(maxTry: 2));
        DownloadPackage package = new() { Storage = new ConcurrentStream(new FullDiskStream(4096), 0) };
        if (asFile)
            package.FileName = GetTempNoFilename();

        // act
        Result result = await Run(service, () => service.DownloadFileTaskAsync(package, DummyFileHelper.GetFileUrl(64 * 1024)));

        // assert
        Assert.True(result.Ended);
        Assert.Null(result.Thrown);
        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(1, result.Events);
        Assert.NotNull(result.Args.Error);
        Assert.Equal(DownloadStatus.Failed, package.Status);
    }

    [Fact]
    public async Task FailedRenameIsReportedOnce()
    {
        // arrange: a folder holds the final name, so moving name.download to it fails
        string target = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "target");
        Directory.CreateDirectory(target);
        DownloadService service = new(new DownloadConfiguration());

        // act
        Result result = await Run(service, () => service.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(4096), target));

        // assert
        Assert.True(result.Ended);
        Assert.Null(result.Thrown);
        Assert.Equal(DownloadStatus.Failed, result.Status);
        Assert.Equal(1, result.Events);
        Assert.NotNull(result.Args.Error);
        Assert.False(File.Exists(target));
    }
}
