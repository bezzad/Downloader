namespace Downloader.Test.UnitTests;

public class DownloadConfigurationTest(ITestOutputHelper output) : BaseTestClass(output)
{
    [Fact]
    public void MaximumSpeedPerChunkTest()
    {
        // arrange
        var configuration =
            new DownloadConfiguration {
                MaximumBytesPerSecond = 10240,
                ParallelDownload = true,
                ChunkCount = 10
            };

        // act
        var maxSpeed = configuration.MaximumSpeedPerChunk;

        // assert
        Assert.Equal(configuration.MaximumBytesPerSecond / configuration.ChunkCount, maxSpeed);
    }

    [Fact]
    public void BufferBlockSizeTest()
    {
        // arrange
        var configuration =
            new DownloadConfiguration {
                MaximumBytesPerSecond = 10240,
                ParallelDownload = true,
                ChunkCount = 10
            };

        // act
        configuration.BufferBlockSize = 10240 * 2;

        // assert
        Assert.Equal(configuration.BufferBlockSize, configuration.MaximumSpeedPerChunk);
    }

    [Fact]
    public void CloneTest()
    {
        // arrange
        var configProperties = typeof(DownloadConfiguration).GetProperties();
        var config = new DownloadConfiguration() {
            MaxTryAgainOnFailover = 100,
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
        var cloneConfig = config.Clone() as DownloadConfiguration;

        // assert
        foreach (PropertyInfo property in configProperties)
        {
            Assert.Equal(property.GetValue(config), property.GetValue(cloneConfig));
        }
    }
}
