using Downloader.DummyHttpServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;

public abstract class ChunkDownloaderTest
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
        Storage.Flush();
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
        using var memoryStream = new MemoryStream(randomlyBytes);
        var pauseToken = new PauseTokenSource();
        var pauseCount = 0;

        // act
        chunkDownloader.DownloadProgressChanged += (sender, e) => {
            if (pauseCount < 10)
            {
                pauseToken.Pause();
                pauseCount++;
                pauseToken.Resume();
            }
        };
        await chunkDownloader.ReadStream(memoryStream, pauseToken.Token, new CancellationToken())
            ;
        Storage.Flush();

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
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        chunkDownloader.DownloadProgressChanged += (s, e) => {
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
        using var slowStream = new ThrottledStream(memoryStream, Configuration.BufferBlockSize);

        // act
        async Task CallReadStream() => await chunkDownloader
            .ReadStream(slowStream, new PauseTokenSource().Token, cts.Token)
            ;

        // assert
        await Assert.ThrowsAnyAsync<TaskCanceledException>(CallReadStream);
    }

    [Fact]
    public async Task CancelReadStreamTest()
    {
        // arrange 
        var stoppedPosition = 0L;
        var randomlyBytes = DummyData.GenerateRandomBytes(Size);
        var cts = new CancellationTokenSource();
        var chunk = new Chunk(0, Size - 1) { Timeout = 1000 };
        var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
        using var memoryStream = new MemoryStream(randomlyBytes);

        // act
        chunkDownloader.DownloadProgressChanged += (sender, e) => {
            if (e.ProgressPercentage > 50)
            {
                cts.Cancel();
                stoppedPosition = e.ReceivedBytesSize;
            }
        };
        async Task act() => await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, cts.Token);
        Storage.Flush();

        // assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
        Assert.False(memoryStream.CanRead); // stream has been closed
        using var chunkStream = Storage.OpenRead();
        for (int i = 0; i < stoppedPosition; i++)
            Assert.Equal(randomlyBytes[i], chunkStream.ReadByte());

        chunkDownloader.Chunk.Clear();
    }
}