namespace Downloader.Test.UnitTests;

public class StorageTestOnFile(ITestOutputHelper output) : StorageTest(output)
{
    private string _path;

    protected override void CreateStorage(int initialSize)
    {
        _path = Path.GetTempFileName();
        Storage = new ConcurrentStream(_path, initialSize);
    }

    public override void Dispose()
    {
        base.Dispose();
        File.Delete(_path);
    }

    [Fact]
    public void TestInitialSizeOnFileStream()
    {
        // act
        CreateStorage(DataLength);

        // assert
        Assert.Equal(DataLength, new FileInfo(_path).Length);
        Assert.Equal(DataLength, Storage.Length);
    }

    [Fact]
    public async Task TestInitialSizeWithNegativeNumberOnFileStream()
    {
        // arrange
        CreateStorage(-1);

        // act
        await Storage.FlushAsync(); // create lazy stream

        // assert
        Assert.Equal(0, new FileInfo(_path).Length);
        Assert.Equal(0, Storage.Length);
    }

    [Fact]
    public async Task TestWriteSizeOverflowOnFileStream()
    {
        // arrange
        int size = 512;
        int actualSize = size * 2;
        byte[] data = new byte[] { 1 };
        CreateStorage(size);

        // act
        for (int i = 0; i < actualSize; i++)
            await Storage.WriteAsync(i, data, 1);

        await Storage.FlushAsync();
        Stream readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(actualSize, new FileInfo(_path).Length);
        Assert.Equal(actualSize, Storage.Length);
        for (int i = 0; i < actualSize; i++)
            Assert.Equal(1, readerStream.ReadByte());
    }

    [Fact]
    public async Task TestAccessMoreThanSizeOnFileStream()
    {
        // arrange
        int size = 10;
        int jumpStepCount = 1024; // 1KB
        byte[] data = new byte[] { 1, 1, 1, 1, 1 };
        int selectedDataLen = 3;
        int actualSize = size + jumpStepCount + selectedDataLen;
        CreateStorage(size);

        // act
        await Storage.WriteAsync(size + jumpStepCount, data, selectedDataLen);
        await Storage.FlushAsync();
        Stream readerStream = Storage.OpenRead();

        // assert
        Assert.Equal(actualSize, new FileInfo(_path).Length);
        Assert.Equal(actualSize, Storage.Length);
        for (int i = 0; i < size + jumpStepCount; i++)
            Assert.Equal(0, readerStream.ReadByte()); // empty spaces

        for (int i = 0; i < selectedDataLen; i++)
            Assert.Equal(1, readerStream.ReadByte()); // wrote data spaces

        Assert.Equal(-1, readerStream.ReadByte()); // end of stream
    }
}