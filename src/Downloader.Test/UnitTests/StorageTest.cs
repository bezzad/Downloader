using System.Buffers;

namespace Downloader.Test.UnitTests;

public abstract class StorageTest(ITestOutputHelper output) : BaseTestClass(output), IDisposable
{
    protected const int DataLength = 2048;
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
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        await Storage.WriteAsync(0, _data, DataLength, true);
        await Storage.FlushAsync();

        // act
        Stream reader = Storage.OpenRead();

        // assert
        Assert.Equal(DataLength, reader.Length);
    }

    [Fact]
    public async Task OpenReadStreamTest()
    {
        // arrange
        CreateStorage(DataLength);
        byte[] _data = DummyData.GenerateRandomBytes(DataLength);
        await Storage.WriteAsync(0, _data, DataLength, false);
        await Storage.FlushAsync();

        // act
        Stream reader = Storage.OpenRead();

        // assert
        for (int i = 0; i < DataLength; i++)
        {
            Assert.Equal(_data[i], reader.ReadByte());
        }

        // clean up
        ArrayPool<byte>.Shared.Return(_data);
    }

    [Fact]
    public async Task SlowWriteTest()
    {
        // arrange
        int size = 1024;
        CreateStorage(size);

        // act
        for (int i = 0; i < size; i++)
        {
            byte[] data = ArrayPool<byte>.Shared.Rent(1);
            data[0] = 1; // fill with non-zero value to verify the write
            await Storage.WriteAsync(i, data, 1, true);
        }

        await Storage.FlushAsync();
        Stream readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(size, Storage.Length);
        for (int i = 0; i < size; i++)
        {
            var val = readerStream.ReadByte();
            Assert.Equal(1, val);
        }
    }

    [Fact]
    public async Task WriteAsyncLengthTest()
    {
        // arrange
        CreateStorage(0);
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        int length = DataLength / 2;

        // act
        await Storage.WriteAsync(0, _data, length, true);
        await Storage.FlushAsync();

        // assert
        Assert.Equal(length, Storage.Length);
    }

    [Fact]
    public async Task WriteAsyncBytesTest()
    {
        // arrange
        CreateStorage(DataLength);
        byte[] _data = DummyData.GenerateRandomBytes(DataLength);
        int length = DataLength / 2;

        // act
        await Storage.WriteAsync(0, _data, length, false);
        await Storage.FlushAsync();
        Stream reader = Storage.OpenRead();

        // assert
        for (int i = 0; i < length; i++)
        {
            Assert.Equal(_data[i], reader.ReadByte());
        }
    }

    [Fact]
    public async Task WriteAsyncMultipleTimeTest()
    {
        // arrange
        CreateStorage(DataLength);
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        int count = 128;
        int size = DataLength / count;

        // act
        for (int i = 0; i < count; i++)
        {
            int startOffset = i * size;
            await Storage.WriteAsync(startOffset, _data.Skip(startOffset).Take(size).ToArray(), size, false);
        }
        await Storage.FlushAsync();

        // assert
        Stream reader = Storage.OpenRead();
        for (int i = 0; i < DataLength; i++)
        {
            Assert.Equal(_data[i], reader.ReadByte());
        }

        // clean up
        ArrayPool<byte>.Shared.Return(_data);
    }

    [Fact]
    public async Task WriteAsyncOutOfRangeExceptionTest()
    {
        // arrange
        CreateStorage(0);
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        int length = DataLength + 1;

        // act
        Func<Task> writeMethod = async () => await Storage.WriteAsync(0, _data, length, false);

        // assert
        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(writeMethod);

        // clean up
        ArrayPool<byte>.Shared.Return(_data);
    }

    [Fact]
    public async Task TestDispose()
    {
        // arrange
        CreateStorage(DataLength);
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        await Storage.WriteAsync(0, _data, DataLength, false);

        // act
        Storage.Dispose();

        // assert
        Assert.ThrowsAny<ObjectDisposedException>(() => Storage.Data);

        // clean up
        ArrayPool<byte>.Shared.Return(_data);
    }

    [Fact]
    public async Task FlushTest()
    {
        // arrange
        CreateStorage(DataLength);
        byte[] _data = DummyData.GenerateRandomSharedBytes(DataLength);
        await Storage.WriteAsync(0, _data, DataLength, true);

        // act
        await Storage.FlushAsync();

        // assert
        Assert.Equal(_data.Length, Storage.Length);
    }

    [Fact]
    public async Task GetLengthTest()
    {
        // arrange
        CreateStorage(0);
        byte[] data = [0x0, 0x1, 0x2, 0x3, 0x4];
        await Storage.WriteAsync(0, data, 1, false);
        await Storage.FlushAsync();

        // act
        long actualLength = Storage.Length;

        // assert
        Assert.Equal(1, actualLength);
    }

    [Fact]
    public async Task TestStreamExpandability()
    {
        // arrange
        CreateStorage(0);
        byte[] data = [0x0, 0x1, 0x2, 0x3, 0x4];
        await Storage.WriteAsync(0, data, data.Length, false);
        await Storage.FlushAsync();

        // act
        string serializedStream = JsonConvert.SerializeObject(Storage);
        Storage.Dispose();
        using ConcurrentStream mutableStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        await mutableStream.WriteAsync(mutableStream.Position, data, data.Length, false);
        await mutableStream.FlushAsync();

        // assert
        Assert.Equal(data.Length * 2, mutableStream?.Length);
    }

    [Fact]
    public async Task TestDynamicBufferData()
    {
        // arrange
        int size = 1024; // 1KB
        byte[] data = new byte[size];
        CreateStorage(size);

        // act
        for (int i = 0; i < size / 8; i++)
        {
            byte[] rent = ArrayPool<byte>.Shared.Rent(16); // zero bytes
            Array.Fill(rent, (byte)i);
            Array.Fill(data, (byte)i, i * 8, 8); // fill only the part to be written
            Output.WriteLine($"Writing {rent.Length} bytes at offset {i * 8}");
            await Storage.WriteAsync(i * 8, rent, 8);
        }
        await Storage.FlushAsync();
        Stream readerStream = Storage.OpenRead();
        byte[] buffer = new byte[size];
        var readCount = await readerStream.ReadAsync(buffer);

        // assert
        Assert.Equal(size, readCount);
        Assert.Equal(data, buffer);
        Assert.Equal(-1, readerStream.ReadByte()); // end of stream
    }

    [Fact]
    public async Task TestArrayPoolCanHandleManyRentWithoutConflict()
    {
        // arrange
        int count = 1000;
        int size = 1024;
        List<int[]> rentedArrays = new();

        // act
        for (int i = 0; i < count; i++)
        {
            // System.Buffers.MemoryPool<byte>.Shared.Rent(size).Memory.ToArray(); // rent and ignore to increase pool pressure
            var array = ArrayPool<int>.Shared.Rent(size);
            Array.Fill(array, i);
            rentedArrays.Add(array);
        }

        // assert
        for (int i = 0; i < count; i++)
        {
            int[] array = rentedArrays[i];
            for (int j = 0; j < size; j++)
            {
                Assert.Equal(i, array[j]);
            }
        }

        // Clean up
        foreach (var array in rentedArrays)
        {
            ArrayPool<int>.Shared.Return(array);
        }
    }

    [Fact]
    public async Task TestSerialization()
    {
        // arrange
        int size = 256;
        byte[] data = DummyData.GenerateOrderedBytes(size);
        CreateStorage(size);
        await Storage.WriteAsync(0, data, size, false);
        await Storage.FlushAsync();

        // act
        string serializedStream = JsonConvert.SerializeObject(Storage);
        await Storage.DisposeAsync();
        await using ConcurrentStream newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
        Stream readerStream = newStream.OpenRead();

        // assert
        Assert.Equal(size, readerStream.Length);
        for (int i = 0; i < size; i++)
            Assert.Equal(i, readerStream.ReadByte());
    }
}