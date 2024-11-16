namespace Downloader.Test.UnitTests;

public abstract class ChunkDownloaderTest(ITestOutputHelper output) : BaseTestClass(output)
{
    protected DownloadConfiguration Configuration { get; set; }
    protected ConcurrentStream Storage { get; set; }
    protected int Size { get; set; } = DummyFileHelper.FileSize16Kb;

    [Fact]
    public async Task ReadStreamTest()
    {
        // arrange
        var randomlyBytes = DummyData.GenerateRandomBytes(Size);
        var chunk = new Chunk(0, Size - 1) { Timeout = 1000 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        using var memoryStream = new MemoryStream(randomlyBytes);

        // act
        await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, new CancellationToken());
        await Storage.FlushAsync();
        var chunkStream = Storage.OpenRead();

        // assert
        Assert.Equal(memoryStream.Length, Storage.Length);
        for (int i = 0; i < Size; i++)
        {
            Assert.Equal(expected: randomlyBytes[i], actual: chunkStream.ReadByte());
        }

        chunkDownloader.Chunk.Clear();
    }

    [Fact]
    public async Task PauseResumeReadStreamTest()
    {
        // arrange            
        var randomlyBytes = DummyData.GenerateRandomBytes(Size);
        var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        using MemoryStream memoryStream = new(randomlyBytes);
        var pauseToken = new PauseTokenSource();
        var pauseCount = 0;

        // act
        chunkDownloader.DownloadProgressChanged += (_, _) => {
            if (pauseCount < 10)
            {
                pauseToken.Pause();
                pauseCount++;
                pauseToken.Resume();
            }
        };
        await chunkDownloader.ReadStream(memoryStream, pauseToken.Token, new CancellationToken());
        await Storage.FlushAsync();

        // assert
        Assert.Equal(memoryStream.Length, Storage.Length);
        Assert.Equal(10, pauseCount);
        var chunkStream = Storage.OpenRead();
        for (int i = 0; i < Size; i++)
            Assert.Equal(randomlyBytes[i], chunkStream.ReadByte());

        chunkDownloader.Chunk.Clear();
    }

    [Fact]
    public async Task ReadStreamProgressEventsTest()
    {
        // arrange
        var eventCount = 0;
        var receivedBytes = new List<byte>();
        var source = DummyData.GenerateRandomBytes(Size);
        using var sourceMemoryStream = new MemoryStream(source);
        var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
        Configuration.EnableLiveStreaming = true;
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        chunkDownloader.DownloadProgressChanged += (_, e) => {
            eventCount++;
            receivedBytes.AddRange(e.ReceivedBytes);
        };

        // act
        await chunkDownloader.ReadStream(sourceMemoryStream, new PauseTokenSource().Token, new CancellationToken());

        // assert
        Assert.Equal(Size / Configuration.BufferBlockSize, eventCount);
        Assert.Equal(chunkDownloader.Chunk.Length, receivedBytes.Count);
        Assert.True(source.SequenceEqual(receivedBytes));

        chunkDownloader.Chunk.Clear();
    }

    [Fact]
    public async Task ReadStreamCanceledExceptionTest()
    {
        // arrange
        var randomlyBytes = DummyData.GenerateRandomBytes(Size);
        var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        using var memoryStream = new MemoryStream(randomlyBytes);
        var canceledToken = new CancellationToken(true);

        // act
        async Task CallReadStream() => await chunkDownloader
            .ReadStream(new MemoryStream(), new PauseTokenSource().Token, canceledToken)
        ;

        // assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(CallReadStream);
    }

    [Fact]
    public async Task ReadStreamTimeoutExceptionTest()
    {
        // arrange
        var cts = new CancellationTokenSource();
        var randomlyBytes = DummyData.GenerateRandomBytes(Size);
        var chunk = new Chunk(0, Size - 1) { Timeout = 0 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        using var memoryStream = new MemoryStream(randomlyBytes);
        var slowStream = new ThrottledStream(memoryStream, Configuration.BufferBlockSize);

        // act
        async Task CallReadStream()
        {
            await chunkDownloader
                .ReadStream(slowStream, new PauseTokenSource().Token, cts.Token);
        }

        // assert
        await Assert.ThrowsAnyAsync<TaskCanceledException>(CallReadStream);

        await slowStream.DisposeAsync();
    }

    [Fact]
    public async Task CancelReadStreamTest()
    {
        // arrange 
        var stoppedPosition = 0L;
        var randomlyBytes = DummyData.GenerateSingleBytes(Size, 200);
        var cts = new CancellationTokenSource();
        var chunk = new Chunk(0, Size - 1) { Id = "Test_Chunk", Timeout = 3000 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        MemoryStream memoryStream = new(randomlyBytes);
        chunkDownloader.DownloadProgressChanged += (_, e) => {
            if (e.ProgressPercentage > 50)
            {
                cts.Cancel();
                stoppedPosition = e.ReceivedBytesSize;
            }
        };

        async Task Act()
        {
            try
            {
                await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, cts.Token);
            }
            finally
            {
                await Storage.FlushAsync();
                //await logger.FlushAsync();
            }
        }

        // act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(Act);
        await using var chunkStream = Storage.OpenRead();

        // assert
        Assert.False(memoryStream.CanRead); // stream has been closed
        Assert.Equal(stoppedPosition, Storage.Length);

        for (int i = 0; i < stoppedPosition; i++)
        {
            var prefix = $"[{i}/{stoppedPosition}] = ";
            var nextValue = chunkStream.ReadByte();
            Assert.Equal(prefix + randomlyBytes[i], prefix + nextValue);
        }

        chunkDownloader.Chunk.Clear();
        await memoryStream.DisposeAsync();
    }

    [Fact]
    public async Task OverflowWhenReadStreamTest()
    {
        // arrange
        byte[] randomlyBytes = DummyData.GenerateRandomBytes(Size);
        Chunk chunk = new(0, (Size / 2) - 1);
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage);
        using MemoryStream memoryStream = new(randomlyBytes);

        // act
        await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, new CancellationToken());
        await Storage.FlushAsync();

        // assert
        Assert.Equal(expected: Size / 2, actual: chunk.Length);
        Assert.Equal(expected: chunk.Length, actual: chunk.Position);
        Assert.Equal(expected: 0, actual: chunk.EmptyLength);
        Assert.Equal(expected: memoryStream.Position, actual: chunk.Position);
        Assert.Equal(expected: chunk.Length, actual: Storage.Length);

        await Storage.DisposeAsync();
    }
}