using Downloader.DummyHttpServer;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;

public abstract class StorageTest : IDisposable
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
        Storage.Flush();

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
        Storage.Flush();

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
        CreateStorage(DataLength);
        var data = new byte[] { 1 };
        var size = 1024;

        // act
        for (int i = 0; i < size; i++)
            await Storage.WriteAsync(i, data, 1);

        Storage.Flush();
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
        CreateStorage(DataLength);
        var length = DataLength / 2;

        // act
        await Storage.WriteAsync(0, Data, length);
        Storage.Flush();

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
        Storage.Flush();
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
        Storage.Flush();

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
        CreateStorage(DataLength);
        var length = DataLength + 1;

        // act
        var writeMethod = async () => await Storage.WriteAsync(0, Data, length);

        // assert
        await Assert.ThrowsAnyAsync<ArgumentException>(writeMethod);
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
        Assert.ThrowsAny<ObjectDisposedException>(() => Storage.Length);
        Assert.ThrowsAny<ObjectDisposedException>(() => Storage.Data);
    }

    [Fact]
    public async Task FlushTest()
    {
        // arrange
        CreateStorage(DataLength);
        await Storage.WriteAsync(0, Data, DataLength);

        // act
        Storage.Flush();

        // assert
        Assert.Equal(Data.Length, Storage.Length);
    }

    [Fact]
    public async Task GetLengthTest()
    {
        // arrange
        CreateStorage(DataLength);
        var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
        await Storage.WriteAsync(0, data, 1);
        Storage.Flush();

        // act
        var actualLength = Storage.Length;

        // assert
        Assert.Equal(1, actualLength);
    }

    [Fact]
    public async Task TestStreamExpandability()
    {
        // arrange
        CreateStorage(DataLength);
        var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
        await Storage.WriteAsync(0, data, data.Length);
        Storage.Flush();

        // act
        var serializedStream = JsonConvert.SerializeObject(Storage);
        var mutableStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        await mutableStream.WriteAsync(mutableStream.Position, data, data.Length);
        mutableStream.Flush();

        // assert
        Assert.Equal(data.Length * 2, mutableStream?.Length);
    }

    [Fact]
    public async Task TestDynamicBufferData()
    {
        // arrange
        CreateStorage(DataLength);
        var size = 1024; // 1KB

        // act
        for (int i = 0; i < size / 8; i++)
        {
            var data = new byte[10]; // zero bytes
            Array.Fill(data, (byte)i);
            await Storage.WriteAsync(i * 8, data, 8);
        }
        Storage.Flush();
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
        CreateStorage(DataLength);
        var size = 256;
        var data = DummyData.GenerateOrderedBytes(size);

        // act
        await Storage.WriteAsync(0, data, size);
        var serializedStream = JsonConvert.SerializeObject(Storage);
        Storage.Dispose();
        var newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        var readerStream = newStream.OpenRead();

        // assert
        Assert.Equal(size, readerStream.Length);
        for (int i = 0; i < size; i++)
            Assert.Equal(i, readerStream.ReadByte());
    }
}