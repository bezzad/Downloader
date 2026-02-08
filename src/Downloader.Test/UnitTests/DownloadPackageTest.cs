namespace Downloader.Test.UnitTests;

public abstract class DownloadPackageTest(ITestOutputHelper output) : BaseTestClass(output), IAsyncLifetime
{
    private byte[] Data { get; set; }
    protected DownloadConfiguration Config { get; set; }
    protected DownloadPackage Package { get; set; }

    public virtual async Task InitializeAsync()
    {
        Config = new DownloadConfiguration { ChunkCount = 8 };
        Data = DummyData.GenerateOrderedBytes(DummyFileHelper.FileSize16Kb);
        Package.BuildStorage(Config.MaximumMemoryBufferBytes, LogFactory?.CreateLogger<DownloadPackage>());
        new ChunkHub(Config).SetFileChunks(Package);
        await Package.Storage.WriteAsync(0, Data, Data.Length);
        await Package.Storage.FlushAsync();
    }

    public virtual Task DisposeAsync()
    {
        Package?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void PackageSerializationTest()
    {
        // act
        string serialized = JsonConvert.SerializeObject(Package);
        Package.Storage.Dispose();
        DownloadPackage deserialized = JsonConvert.DeserializeObject<DownloadPackage>(serialized);
        byte[] destData = new byte[deserialized.TotalFileSize];
        _ = deserialized.Storage.OpenRead().Read(destData, 0, destData.Length);

        // assert
        AssertHelper.AreEquals(Package, deserialized);
        Assert.True(Data.SequenceEqual(destData));

        deserialized.ClearChunks();
        deserialized.Storage.Dispose();
    }

    [Fact]
    public void ClearChunksTest()
    {
        // act
        Package.ClearChunks();

        // assert
        Assert.Null(Package.Chunks);
    }

    [Fact]
    public void ClearPackageTest()
    {
        // act
        Package.ClearChunks();

        // assert
        Assert.Equal(0, Package.ReceivedBytesSize);
    }

    [Fact]
    public void PackageValidateTest()
    {
        // arrange
        Package.Chunks[0].Position = Package.Storage.Length;

        // act
        Package.Validate();

        // assert
        Assert.Equal(0, Package.Chunks[0].Position);
    }

    [Fact]
    public void TestPackageValidateWhenDoesNotSupportDownloadInRange()
    {
        // arrange
        Package.Chunks[0].Position = 1000;
        Package.IsSupportDownloadInRange = false;

        // act
        Package.Validate();

        // assert
        Assert.Equal(0, Package.Chunks[0].Position);
    }
}