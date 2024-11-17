namespace Downloader.Test.UnitTests;

public abstract class StorageTest(ITestOutputHelper output) : BaseTestClass(output), IDisposable
{
    protected const int DataLength = 2048;
    protected readonly byte[] Data = DummyData.GenerateRandomBytes(DataLength);
    protected ConcurrentStream Storage;

    protected abstract void CreateStorage(int initialSize);

    public virtual void Dispose()
    {
        Storage?.Dispose();
    }

    [Fact]
    public async Task OpenReadLengthTest()
    {
        // arrange
        CreateStorage(DataLength);
        await Storage.WriteAsync(0, Data, DataLength);
        await Storage.FlushAsync();

        // act
        var reader = Storage.OpenRead();

        // assert
        Assert.Equal(DataLength, reader.Length);
    }

    [Fact]
    public async Task OpenReadStreamTest()
    {
        // arrange
        CreateStorage(DataLength);
        await Storage.WriteAsync(0, Data, DataLength);
        await Storage.FlushAsync();

        // act
        var reader = Storage.OpenRead();

        // assert
        for (int i = 0; i < DataLength; i++)
        {
            Assert.Equal(Data[i], reader.ReadByte());
        }
    }

    [Fact]
    public async Task SlowWriteTest()
    {
        // arrange
        var data = new byte[] { 1 };
        var size = 1024;
        CreateStorage(size);

        // act
        for (int i = 0; i < size; i++)
            await Storage.WriteAsync(i, data, 1);

        await Storage.FlushAsync();
        var readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(size, Storage.Length);
        for (int i = 0; i < size; i++)
            Assert.Equal(1, readerStream.ReadByte());
    }

    [Fact]
    public async Task WriteAsyncLengthTest()
    {
        // arrange
        CreateStorage(0);
        var length = DataLength / 2;

        // act
        await Storage.WriteAsync(0, Data, length);
        await Storage.FlushAsync();

        // assert
        Assert.Equal(length, Storage.Length);
    }

    [Fact]
    public async Task WriteAsyncBytesTest()
    {
        // arrange
        CreateStorage(DataLength);
        var length = DataLength / 2;

        // act
        await Storage.WriteAsync(0, Data, length);
        await Storage.FlushAsync();
        var reader = Storage.OpenRead();

        // assert
        for (int i = 0; i < length; i++)
        {
            Assert.Equal(Data[i], reader.ReadByte());
        }
    }

    [Fact]
    public async Task WriteAsyncMultipleTimeTest()
    {
        // arrange
        CreateStorage(DataLength);
        var count = 128;
        var size = DataLength / count;

        // act
        for (int i = 0; i < count; i++)
        {
            var startOffset = i * size;
            await Storage.WriteAsync(startOffset, Data.Skip(startOffset).Take(size).ToArray(), size);
        }
        await Storage.FlushAsync();

        // assert
        var reader = Storage.OpenRead();
        for (int i = 0; i < DataLength; i++)
        {
            Assert.Equal(Data[i], reader.ReadByte());
        }
    }

    [Fact]
    public async Task WriteAsyncOutOfRangeExceptionTest()
    {
        // arrange
        CreateStorage(0);
        var length = DataLength + 1;

        // act
        var writeMethod = async () => await Storage.WriteAsync(0, Data, length);

        // assert
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(writeMethod);
    }

    [Fact]
    public async Task TestDispose()
    {
        // arrange
        CreateStorage(DataLength);
        await Storage.WriteAsync(0, Data, DataLength);

        // act
        Storage.Dispose();

        // assert
        Assert.ThrowsAny<ObjectDisposedException>(() => Storage.Data);
    }

    [Fact]
    public async Task FlushTest()
    {
        // arrange
        CreateStorage(DataLength);
        await Storage.WriteAsync(0, Data, DataLength);

        // act
        await Storage.FlushAsync();

        // assert
        Assert.Equal(Data.Length, Storage.Length);
    }

    [Fact]
    public async Task GetLengthTest()
    {
        // arrange
        CreateStorage(0);
        var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
        await Storage.WriteAsync(0, data, 1);
        await Storage.FlushAsync();

        // act
        var actualLength = Storage.Length;

        // assert
        Assert.Equal(1, actualLength);
    }

    [Fact]
    public async Task TestStreamExpandability()
    {
        // arrange
        CreateStorage(0);
        var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
        await Storage.WriteAsync(0, data, data.Length);
        await Storage.FlushAsync();

        // act
        var serializedStream = JsonConvert.SerializeObject(Storage);
        Storage.Dispose();
        using var mutableStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        await mutableStream.WriteAsync(mutableStream.Position, data, data.Length);
        await mutableStream.FlushAsync();

        // assert
        Assert.Equal(data.Length * 2, mutableStream?.Length);
    }

    [Fact]
    public async Task TestDynamicBufferData()
    {
        // arrange
        var size = 1024; // 1KB
        CreateStorage(size);

        // act
        for (int i = 0; i < size / 8; i++)
        {
            var data = new byte[10]; // zero bytes
            Array.Fill(data, (byte)i);
            await Storage.WriteAsync(i * 8, data, 8);
        }
        await Storage.FlushAsync();
        var readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(size, Storage.Length);
        for (int i = 0; i < size / 8; i++)
        {
            var data = new byte[8]; // zero bytes
            Array.Fill(data, (byte)i);
            var buffer = new byte[8];
            Assert.Equal(8, readerStream.Read(buffer, 0, 8));
            Assert.True(buffer.SequenceEqual(data));
        }

        Assert.Equal(-1, readerStream.ReadByte()); // end of stream
    }

    [Fact]
    public async Task TestSerialization()
    {
        // arrange
        var size = 256;
        var data = DummyData.GenerateOrderedBytes(size);
        CreateStorage(size);
        await Storage.WriteAsync(0, data, size);
        await Storage.FlushAsync();

        // act
        var serializedStream = JsonConvert.SerializeObject(Storage);
        Storage.Dispose();
        using var newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        var readerStream = newStream.OpenRead();

        // assert
        Assert.Equal(size, readerStream.Length);
        for (int i = 0; i < size; i++)
            Assert.Equal(i, readerStream.ReadByte());
    }
}