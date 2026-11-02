namespace Downloader.Test.UnitTests;

public class ConcurrentStreamTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private sealed class FailingWriteStream : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new IOException("disk is full");
        }
    }

    [Fact]
    public async Task WriteGoesToTheGivenStream()
    {
        // arrange
        var baseStream = new MemoryStream();
        await using var stream = new ConcurrentStream(baseStream, 0);
        byte[] data = [1, 2, 3, 4];

        // act
        await stream.WriteAsync(0, data, data.Length, false);
        await stream.FlushAsync();

        // assert
        Assert.Equal(data, baseStream.ToArray());
        Assert.True(stream.IsMemoryStream);
    }

    [Fact]
    public async Task SyncWriteAndReadBack()
    {
        // arrange
        await using var stream = new ConcurrentStream();
        byte[] data = [10, 20, 30, 40, 50];
        byte[] read = new byte[data.Length];

        // act
        stream.Write(0, data, data.Length, false);
        await stream.FlushAsync();
        int count = stream.Read(read, 0, read.Length);

        // assert
        Assert.Equal(data.Length, count);
        Assert.Equal(data, read);
    }

    [Fact]
    public async Task ReadAsyncReturnsWrittenBytes()
    {
        // arrange
        await using var stream = new ConcurrentStream();
        byte[] data = [7, 8, 9];
        byte[] read = new byte[data.Length];

        // act
        await stream.WriteAsync(0, data, data.Length, false);
        await stream.FlushAsync();
        int count = await stream.ReadAsync(read.AsMemory());

        // assert
        Assert.Equal(data.Length, count);
        Assert.Equal(data, read);
    }

    [Fact]
    public void SyncWriteWithTooLongLengthThrows()
    {
        // arrange
        using var stream = new ConcurrentStream();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => stream.Write(0, new byte[2], 3, false));
    }

    [Fact]
    public void DataThrowsAfterDispose()
    {
        // arrange
        var stream = new ConcurrentStream();

        // act
        stream.Dispose();
        stream.Dispose(); // the second call does nothing

        // assert
        Assert.True(stream.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => stream.Data);
    }

    [Fact]
    public void DataSetToNullDoesNothing()
    {
        // arrange
        using var stream = new ConcurrentStream { Data = [1, 2] };

        // act
        stream.Data = null;

        // assert
        Assert.Equal([1, 2], stream.Data);
    }

    [Fact]
    public void DataIsNullForFileStream()
    {
        // arrange
        string path = Path.GetTempFileName();
        try
        {
            using var stream = new ConcurrentStream(path, 0, 0);

            // act & assert
            Assert.False(stream.IsMemoryStream);
            Assert.Null(stream.Data);
            Assert.True(stream.CanRead);
            Assert.True(stream.CanWrite);
            Assert.True(stream.CanSeek);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void PositionIsKeptUntilTheStreamIsCreated()
    {
        // arrange
        using var stream = new ConcurrentStream();

        // act
        stream.Position = 5; // the memory stream is not created yet

        // assert
        Assert.Equal(5, stream.Position);
    }

    [Fact]
    public void PositionIsAppliedWhenTheFileStreamIsCreated()
    {
        // arrange
        string path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[10]);
        try
        {
            // a deserialized stream has a path and a position, but no base stream yet
            var stream = new ConcurrentStream { Path = path };
            stream.Position = 4;

            // act
            stream.SetLength(10); // creates the base stream

            // assert
            Assert.Equal(4, stream.Position);
            stream.Dispose();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteErrorFaultsTheStream()
    {
        // arrange
        await using var stream = new ConcurrentStream(new FailingWriteStream(), 0);
        byte[] data = [1, 2, 3];

        // act
        await stream.WriteAsync(0, data, data.Length, false);
        // Wait for the background writer. Note: FlushAsync can return before the error is set,
        // because the queue is empty again before the writer records the error.
        for (int i = 0; i < 500 && !stream.IsFaulted; i++)
            await Task.Delay(10);

        // assert
        Assert.True(stream.IsFaulted);
        Assert.IsType<IOException>(stream.Exception.InnerException);
        await Assert.ThrowsAsync<AggregateException>(() => stream.WriteAsync(0, data, data.Length, false));
        Assert.Throws<AggregateException>(() => stream.Write(0, data, data.Length, false));
        await Assert.ThrowsAsync<AggregateException>(() => stream.FlushAsync());
    }

    [Fact]
    public async Task DisposeAsyncTwiceIsSafe()
    {
        // arrange
        var stream = new ConcurrentStream(new MemoryStream(), 0);

        // act
        await stream.DisposeAsync();
        await stream.DisposeAsync();

        // assert
        Assert.True(stream.IsDisposed);
    }

    [Fact]
    public async Task DisposeAsyncWithoutBaseStream()
    {
        // arrange
        var stream = new ConcurrentStream();

        // act
        await stream.DisposeAsync();

        // assert
        Assert.True(stream.IsDisposed);
        Assert.Equal(0, stream.Length);
    }

    [Fact]
    public async Task WriteAfterDisposeThrows()
    {
        // arrange
        var stream = new ConcurrentStream(new MemoryStream(), 0);
        await stream.DisposeAsync();

        // act & assert: the caller must know the bytes were not written
        await Assert.ThrowsAsync<ObjectDisposedException>(() => stream.WriteAsync(0, new byte[4], 4, false));
        Assert.Throws<ObjectDisposedException>(() => stream.Write(0, new byte[4], 4, false));
    }
}
