namespace Downloader.Test.UnitTests;

public class StorageTestOnMemory(ITestOutputHelper output) : StorageTest(output)
{
    protected override void CreateStorage(int initialSize)
    {
        Storage = new ConcurrentStream(null);
    }

    [Fact]
    public void TestInitialSizeOnMemoryStream()
    {
        // act
        CreateStorage(0);
        using Stream stream = Storage.OpenRead();

        // assert
        Assert.IsType<MemoryStream>(stream);
    }
}
