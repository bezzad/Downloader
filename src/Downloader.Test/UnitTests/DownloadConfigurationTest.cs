namespace Downloader.Test.UnitTests;

public class DownloadConfigurationTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Theory]
    [InlineData(10, 5, 2)]
    [InlineData(10, 2, 2)]
    [InlineData(10, 4, 4)]
    [InlineData(10, 10, 4)]
    [InlineData(10, 10, 10)]
    [InlineData(10, 10, 1)]
    [InlineData(10, 20, 1)]
    [InlineData(10, 20, 2)]
    [InlineData(10, 20, 4)]
    public void MaximumSpeedPerChunkTest(int chunks, int parallelCount, int activeCount)
    {
        // arrange
        DownloadConfiguration configuration =
            new() {
                MaximumBytesPerSecond = 10240,
                ParallelDownload = true,
                ChunkCount = chunks, 
                ParallelCount = parallelCount,
                ActiveChunks = activeCount
            };

        // act
        long expectedSpeed = configuration.MaximumBytesPerSecond / Math.Max(Math.Min(Math.Min(chunks, parallelCount), activeCount), 1);

        // assert
        Assert.Equal(expectedSpeed, configuration.MaximumSpeedPerChunk);
    }

    [Fact]
    public void BufferBlockSizeTest()
    {
        // arrange
        DownloadConfiguration configuration =
            new() {
                MaximumBytesPerSecond = 10240,
                ParallelDownload = true,
                ChunkCount = 10,
                // act
                BufferBlockSize = 10240 * 2
            };

        // assert
        Assert.Equal(configuration.BufferBlockSize, configuration.MaximumSpeedPerChunk);
    }

    [Fact]
    public void CloneTest()
    {
        // arrange
        PropertyInfo[] configProperties = typeof(DownloadConfiguration).GetProperties();
        DownloadConfiguration config = new() {
            MaxTryAgainOnFailure = 100,
            ParallelDownload = true,
            ChunkCount = 1,
            Timeout = 150,
            BufferBlockSize = 2048,
            MaximumBytesPerSecond = 1024,
            RequestConfiguration = new RequestConfiguration(),
            CheckDiskSizeBeforeDownload = false,
            MinimumSizeOfChunking = 1024,
            ClearPackageOnCompletionWithFailure = true,
            ReserveStorageSpaceBeforeStartingDownload = true,
            EnableLiveStreaming = true,
            RangeDownload = true,
            RangeHigh = 102400,
            RangeLow = 10240
        };

        // act
        DownloadConfiguration cloneConfig = config.Clone() as DownloadConfiguration;

        // assert
        foreach (PropertyInfo property in configProperties)
        {
            Assert.Equal(property.GetValue(config), property.GetValue(cloneConfig));
        }
    }
}
