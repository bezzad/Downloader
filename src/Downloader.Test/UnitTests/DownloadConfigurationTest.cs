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
            BlockTimeout = 150,
            BufferBlockSize = 2048,
            MaximumBytesPerSecond = 1024,
            RequestConfiguration = new RequestConfiguration(),
            CheckDiskSizeBeforeDownload = false,
            MinimumSizeOfChunking = 1024,
            ClearPackageOnCompletionWithFailure = true,
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
    
    [Theory]
    [InlineData(".test")]
    [InlineData(".TEST")]
    [InlineData("test")]
    [InlineData("TEST")]
    public void DownloadingFileExtensionTest(string extension)
    {
        // arrange
        DownloadConfiguration config = new() {
            DownloadFileExtension = extension
        };

        // act
        string actualExtension = config.DownloadFileExtension;

        // assert
        Assert.Equal(".test", actualExtension);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1048577)]
    public void BufferBlockSizeOutOfRangeThrows(int size)
    {
        // arrange
        DownloadConfiguration config = new();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.BufferBlockSize = size);
    }

    [Fact]
    public void NegativeMaximumBytesPerSecondMeansUnlimited()
    {
        // arrange
        DownloadConfiguration config = new() { MaximumBytesPerSecond = 100 };

        // act
        config.MaximumBytesPerSecond = -1;

        // assert
        Assert.Equal(long.MaxValue, config.MaximumBytesPerSecond);
    }

    [Fact]
    public void InvalidValuesThrow()
    {
        // arrange
        DownloadConfiguration config = new();

        // act & assert
        Assert.Throws<ArgumentOutOfRangeException>(() => config.RangeHigh = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.BlockTimeout = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.HttpClientTimeout = 999);
        Assert.Throws<ArgumentOutOfRangeException>(() => config.MaximumMemoryBufferBytes = -1);
        Assert.Throws<ArgumentException>(() => config.DownloadFileExtension = " ");
    }

    [Fact]
    public void PropertyChangedFiresOnlyOnRealChange()
    {
        // arrange
        DownloadConfiguration config = new() { ChunkCount = 2, MaximumBytesPerSecond = 10, BufferBlockSize = 10 };
        List<string> changed = [];
        config.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        // act
        config.ChunkCount = 2; // same value
        config.MaximumBytesPerSecond = 10; // same value
        config.BufferBlockSize = 10; // same value
        config.ChunkCount = 3;
        config.MaximumBytesPerSecond = 20;
        config.BufferBlockSize = 20;

        // assert
        Assert.Equal([nameof(config.ChunkCount), nameof(config.MaximumBytesPerSecond), nameof(config.BufferBlockSize)], changed);
    }

    [Fact]
    public void PropertySetWithoutListenersDoesNotThrow()
    {
        // arrange
        DownloadConfiguration config = new();

        // act
        config.ChunkCount = 5;
        config.MaximumBytesPerSecond = 5;
        config.BufferBlockSize = 5;

        // assert
        Assert.Equal(5, config.ChunkCount);
    }
}
