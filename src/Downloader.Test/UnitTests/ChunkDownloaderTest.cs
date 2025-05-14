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
        byte[] randomlyBytes = DummyData.GenerateRandomBytes(Size);
        Chunk chunk = new(0, Size - 1) { Timeout = 1000 };
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        using MemoryStream memoryStream = new(randomlyBytes);

        // act
        await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, new CancellationToken());
        await Storage.FlushAsync();
        Stream chunkStream = Storage.OpenRead();

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
        byte[] randomlyBytes = DummyData.GenerateRandomBytes(Size);
        Chunk chunk = new(0, Size - 1) { Timeout = 100 };
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        using MemoryStream memoryStream = new(randomlyBytes);
        PauseTokenSource pauseToken = new();
        int pauseCount = 0;

        // act
        chunkDownloader.DownloadProgressChanged += (_, _) => {
            if (pauseCount < 10)
            {
                pauseToken.Pause();
                pauseCount++;
                pauseToken.Resume();
            }
        };
        await chunkDownloader.ReadStream(memoryStream, pauseToken.Token, CancellationToken.None);
        await Storage.FlushAsync();

        // assert
        Assert.Equal(memoryStream.Length, Storage.Length);
        Stream chunkStream = Storage.OpenRead();
        for (int i = 0; i < Size; i++)
            Assert.Equal(randomlyBytes[i], chunkStream.ReadByte());

        chunkDownloader.Chunk.Clear();
    }

    [Fact]
    public async Task ReadStreamProgressEventsTest()
    {
        // arrange
        int eventCount = 0;
        List<byte> receivedBytes = new();
        byte[] source = DummyData.GenerateRandomBytes(Size);
        using MemoryStream sourceMemoryStream = new(source);
        Chunk chunk = new(0, Size - 1) { Timeout = 100 };
        Configuration.EnableLiveStreaming = true;
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        chunkDownloader.DownloadProgressChanged += (_, e) => {
            eventCount++;
            receivedBytes.AddRange(e.ReceivedBytes);
        };

        // act
        await chunkDownloader.ReadStream(sourceMemoryStream, new PauseTokenSource().Token, CancellationToken.None);

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
        byte[] randomlyBytes = DummyData.GenerateRandomBytes(Size);
        Chunk chunk = new(0, Size - 1) { Timeout = 100 };
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        using MemoryStream memoryStream = new(randomlyBytes);
        CancellationToken canceledToken = new(true);

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
        CancellationTokenSource cts = new();
        byte[] randomlyBytes = DummyData.GenerateRandomBytes(Size);
        Chunk chunk = new(0, Size - 1) { Timeout = 0 };
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        using MemoryStream memoryStream = new(randomlyBytes);
        ThrottledStream slowStream = new(memoryStream, Configuration.BufferBlockSize);

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
        long stoppedPosition = 0L;
        byte[] randomlyBytes = DummyData.GenerateSingleBytes(Size, 200);
        CancellationTokenSource cts = new();
        Chunk chunk = new(0, Size - 1) { Id = "Test_Chunk", Timeout = 3000 };
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
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
        await using Stream chunkStream = Storage.OpenRead();

        // assert
        Assert.False(memoryStream.CanRead); // stream has been closed
        Assert.Equal(stoppedPosition, Storage.Length);

        for (int i = 0; i < stoppedPosition; i++)
        {
            string prefix = $"[{i}/{stoppedPosition}] = ";
            int nextValue = chunkStream.ReadByte();
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
        ChunkDownloader chunkDownloader = new(chunk, Configuration, Storage, new SocketClient(Configuration.RequestConfiguration));
        using MemoryStream memoryStream = new(randomlyBytes);

        // act
        await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, CancellationToken.None);
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