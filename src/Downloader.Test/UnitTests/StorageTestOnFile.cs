using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Downloader.Test.UnitTests;

public class StorageTestOnFile : StorageTest
{
    private string path;
    
    protected override void CreateStorage(int initialSize)
    {
        path = Path.GetTempFileName();
        Storage = new ConcurrentStream(path, initialSize);
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
        CreateStorage(DataLength);

        // assert
        Assert.Equal(DataLength, new FileInfo(path).Length);
        Assert.Equal(DataLength, Storage.Length);
    }

    [Fact]
    public void TestInitialSizeWithNegativeNumberOnFileStream()
    {
        // arrange
        CreateStorage(-1);

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
        var size = 512;
        var actualSize = size * 2;
        var data = new byte[] { 1 };
        CreateStorage(size);

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
        var size = 10;
        var jumpStepCount = 1024; // 1KB
        var data = new byte[] { 1, 1, 1, 1, 1 };
        var selectedDataLen = 3;
        var actualSize = size + jumpStepCount + selectedDataLen;
        CreateStorage(size);

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
