using System.IO;
using Xunit;

namespace Downloader.Test.UnitTests;

public class StorageTestOnMemory : StorageTest
{
    private ConcurrentStream _storage;
    protected override ConcurrentStream Storage => _storage ??= new ConcurrentStream();

    [Fact]
    public void TestInitialSizeOnMemoryStream()
    {
        // assert
        Assert.IsType<MemoryStream>(Storage.OpenRead());
    }
}
