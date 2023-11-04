using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;

public class StorageTestOnFile : StorageTest
{
    private string path;
    private int size;
    private ConcurrentStream _storage;
    protected override ConcurrentStream Storage => _storage ??= new ConcurrentStream(path, size);

    public StorageTestOnFile()
    {
        size = 1024 * 1024; // 1MB
        path = Path.GetTempFileName();
    }

    public override void Dispose()
    {
        base.Dispose();
        File.Delete(path);
    }

    [Fact]
    public void TestInitialSizeOnFileStream()
    {
        // act
        var Storage = new ConcurrentStream(path, size);

        // assert
        Assert.Equal(size, new FileInfo(path).Length);
        Assert.Equal(size, Storage.Length);
    }

    [Fact]
    public void TestInitialSizeWithNegativeNumberOnFileStream()
    {
        // arrange
        size = -1;

        // act
        Storage.Flush(); // create lazy stream

        // assert
        Assert.Equal(0, new FileInfo(path).Length);
        Assert.Equal(0, Storage.Length);
    }

    [Fact]
    public async Task TestWriteSizeOverflowOnFileStream()
    {
        // arrange
        size = 512;
        var actualSize = size * 2;
        var data = new byte[] { 1 };

        // act
        for (int i = 0; i < actualSize; i++)
            await Storage.WriteAsync(i, data, 1);

        Storage.Flush();
        var readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(actualSize, new FileInfo(path).Length);
        Assert.Equal(actualSize, Storage.Length);
        for (int i = 0; i < actualSize; i++)
            Assert.Equal(1, readerStream.ReadByte());
    }

    [Fact]
    public async Task TestAccessMoreThanSizeOnFileStream()
    {
        // arrange
        size = 10;
        var jumpStepCount = 1024; // 1KB
        var data = new byte[] { 1, 1, 1, 1, 1 };
        var selectedDataLen = 3;
        var actualSize = size + jumpStepCount + selectedDataLen;

        // act
        await Storage.WriteAsync(size + jumpStepCount, data, selectedDataLen);
        Storage.Flush();
        var readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(actualSize, new FileInfo(path).Length);
        Assert.Equal(actualSize, Storage.Length);
        for (int i = 0; i < size + jumpStepCount; i++)
            Assert.Equal(0, readerStream.ReadByte()); // empty spaces

        for (int i = 0; i < selectedDataLen; i++)
            Assert.Equal(1, readerStream.ReadByte()); // wrote data spaces

        Assert.Equal(-1, readerStream.ReadByte()); // end of stream
    }
}
