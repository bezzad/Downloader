using System.IO;
using Xunit;

namespace Downloader.Test.UnitTests;

public class StorageTestOnMemory : StorageTest
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
        using var stream = Storage.OpenRead();

        // assert
        Assert.IsType<MemoryStream>(stream);
    }
}
