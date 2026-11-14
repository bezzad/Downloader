namespace Downloader.Test.UnitTests;

/// <summary>
/// Issue #259: a resume save does "take the chunk positions, flush, write them". For that the flush must
/// end soon while the download keeps writing, must cover every byte given before it, and must put the
/// bytes on the disk.
/// </summary>
public class ConcurrentStreamFlushTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private static readonly TimeSpan FlushLimit = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A disk that is slower than the network: each write takes some time. It records where it wrote.
    /// </summary>
    private sealed class SlowStream(int writeDelayMs) : MemoryStream
    {
        private readonly HashSet<long> _written = [];

        public bool HasWritten(long position)
        {
            lock (_written)
                return _written.Contains(position);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(writeDelayMs, cancellationToken);
            long position = Position;
            await base.WriteAsync(buffer, cancellationToken);
            lock (_written)
                _written.Add(position);
        }
    }

    /// <summary>
    /// A disk that fails after a short wait, so a flush is already waiting when it fails.
    /// </summary>
    private sealed class LateFailingStream : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken);
            throw new IOException("disk is full");
        }
    }

    /// <summary>
    /// A file stream that records whether a flush asked for the bytes to go to the disk.
    /// </summary>
    private sealed class DiskFlushRecordingStream(string path)
        : FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite)
    {
        public bool FlushedToDisk { get; private set; }

        public override void Flush(bool flushToDisk)
        {
            FlushedToDisk |= flushToDisk;
            base.Flush(flushToDisk);
        }
    }

    /// <summary>
    /// Like a running download: keeps writing blocks after <paramref name="firstPosition"/> until stopped.
    /// </summary>
    private static async Task KeepWriting(ConcurrentStream stream, long firstPosition, CancellationToken stop)
    {
        byte[] block = new byte[16];
        for (long position = firstPosition; !stop.IsCancellationRequested; position += block.Length)
        {
            await stream.WriteAsync(position, block, block.Length, false);
            await Task.Delay(1, CancellationToken.None);
        }
    }

    private static async Task<bool> EndsInTime(Task task)
    {
        return await Task.WhenAny(task, Task.Delay(FlushLimit)) == task;
    }

    [Fact]
    public async Task FlushAsyncEndsWhileOtherWritesKeepComing()
    {
        // arrange
        var baseStream = new SlowStream(writeDelayMs: 5);
        await using var stream = new ConcurrentStream(baseStream, 0);
        using var stopWriter = new CancellationTokenSource();
        Task writer = KeepWriting(stream, 0, stopWriter.Token);
        await Task.Delay(100); // the queue is never empty from now on

        try
        {
            // act
            Task flush = stream.FlushAsync();

            // assert — the flush waits for what is queued now, not for a queue that never gets empty.
            Assert.True(await EndsInTime(flush), "FlushAsync did not end while the writer kept writing.");
        }
        finally
        {
            await stopWriter.CancelAsync();
            await writer;
        }
    }

    [Fact]
    public async Task FlushAsyncWritesEveryBlockGivenBeforeIt()
    {
        // arrange — the positions a resume save would store must be on the storage after the flush.
        var baseStream = new SlowStream(writeDelayMs: 5);
        await using var stream = new ConcurrentStream(baseStream, 0);
        const int blocksBeforeFlush = 20;
        byte[] block = new byte[16];
        for (int i = 0; i < blocksBeforeFlush; i++)
            await stream.WriteAsync(i * block.Length, block, block.Length, false);

        using var stopWriter = new CancellationTokenSource();
        Task writer = KeepWriting(stream, blocksBeforeFlush * block.Length, stopWriter.Token);

        try
        {
            // act
            Task flush = stream.FlushAsync();
            bool ended = await EndsInTime(flush);
            int[] missing = Enumerable.Range(0, blocksBeforeFlush)
                .Where(i => !baseStream.HasWritten(i * block.Length)).ToArray();

            // assert
            Assert.True(ended, "FlushAsync did not end while the writer kept writing.");
            Assert.Empty(missing);
        }
        finally
        {
            await stopWriter.CancelAsync();
            await writer;
        }
    }

    [Fact]
    public async Task PackageFlushAsyncEndsWhileChunksKeepWriting()
    {
        // arrange — the public flush a caller uses before saving the package.
        await using var package = new DownloadPackage();
        package.Storage = new ConcurrentStream(new SlowStream(writeDelayMs: 5), 0);
        using var stopWriter = new CancellationTokenSource();
        Task writer = KeepWriting(package.Storage, 0, stopWriter.Token);
        await Task.Delay(100);

        try
        {
            // act
            Task flush = package.FlushAsync();

            // assert
            Assert.True(await EndsInTime(flush), "DownloadPackage.FlushAsync did not end while chunks kept writing.");
        }
        finally
        {
            await stopWriter.CancelAsync();
            await writer;
        }
    }

    [Fact]
    public async Task FlushAsyncOnAFileStreamWritesToTheDisk()
    {
        // arrange
        string path = Path.GetTempFileName();
        try
        {
            var fileStream = new DiskFlushRecordingStream(path);
            await using var stream = new ConcurrentStream(fileStream, 0);
            byte[] data = [1, 2, 3, 4];
            await stream.WriteAsync(0, data, data.Length, false);

            // act
            await stream.FlushAsync();

            // assert — Flush(false) leaves the bytes in the OS cache; a power cut loses them.
            Assert.True(fileStream.FlushedToDisk, "FlushAsync did not call FileStream.Flush(true).");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlushAsyncDoesNotReportSuccessWhenTheStreamIsClosedBeforeTheBytesAreWritten()
    {
        // arrange — the disk is slow, so the blocks are still queued when the stream is closed.
        var baseStream = new SlowStream(writeDelayMs: 200);
        var stream = new ConcurrentStream(baseStream, 0);
        byte[] block = new byte[16];
        for (int i = 0; i < 5; i++)
            await stream.WriteAsync(i * block.Length, block, block.Length, false);
        Task flush = stream.FlushAsync();

        // act
        await stream.DisposeAsync();

        // assert — the flush must end, and must not say "written" for bytes that were dropped.
        Assert.True(await EndsInTime(flush), "FlushAsync hangs after the stream is closed.");
        Exception error = await Record.ExceptionAsync(() => flush);
        Assert.True(error is OperationCanceledException or ObjectDisposedException,
            $"FlushAsync reported success, but {Enumerable.Range(0, 5).Count(i => !baseStream.HasWritten(i * block.Length))} " +
            "of 5 queued blocks were not written. Error: " + (error?.GetType().Name ?? "none"));
    }

    [Fact]
    public async Task FlushAsyncThrowsTheWriterErrorWhenTheWriterFailsDuringTheFlush()
    {
        // arrange
        await using var stream = new ConcurrentStream(new LateFailingStream(), 0);
        byte[] block = new byte[16];
        await stream.WriteAsync(0, block, block.Length, false);

        // act
        Task flush = stream.FlushAsync();

        // assert — the flush ends with the writer's error, not with success and not never.
        Assert.True(await EndsInTime(flush), "FlushAsync hangs after the writer failed.");
        var error = await Assert.ThrowsAsync<AggregateException>(() => flush);
        Assert.IsType<IOException>(error.InnerException);
    }
}
