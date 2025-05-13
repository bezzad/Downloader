namespace Downloader.Test.UnitTests;

public class ChunkHubTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly DownloadConfiguration _config = new() {
        Timeout = 100,
        MaxTryAgainOnFailure = 100,
        BufferBlockSize = 1024
    };

    [Theory]
    [InlineData(1, 1024)] // Chunk File Positive 1 Parts Test
    public void SingleChunkFileTest(int chunkCount, long fileSize)
    {
        // act 
        using DownloadPackage package = ChunkFileTest(chunkCount, fileSize);

        // assert
        Assert.Single(package.Chunks);
    }
    
    [Theory]
    [InlineData(-10, 1024)] // Chunk File By Negative Parts Test
    [InlineData(-1, 1024)] // Chunk File By Negative Parts Test
    [InlineData(0, 1024)] // Chunk File By Zero Parts Test
    public void ChunkFileWithErrorTest(int chunkCount, long fileSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChunkFileTest(chunkCount, fileSize));
    }

    [Theory]
    [InlineData(8, 1024)]
    [InlineData(256, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(64, 10679630)]
    public void PositiveChunkFileTest(int chunkCount, long fileSize)
    {
        // act 
        using DownloadPackage package = ChunkFileTest(chunkCount, fileSize);

        // assert
        Assert.Equal(chunkCount, package.Chunks.Length);
        Assert.Equal(fileSize, package.Chunks.Sum(chunk => chunk.Length));
    }

    [Theory]
    [InlineData(1030, 1024)]
    public void ChunkFileMoreThanSizeTest(int chunkCount, long fileSize)
    {
        // act 
        using DownloadPackage package = ChunkFileTest(chunkCount, fileSize);

        // assert
        Assert.Equal(fileSize, package.Chunks.Length);
    }

    [Fact]
    public void ChunkFileWithRangeSizeTest()
    {
        // arrange
        int fileSize = 10679630;
        _config.RangeLow = 1024;
        _config.RangeHigh = 9679630;
        long totalBytes = _config.RangeHigh - _config.RangeLow + 1;

        // act
        using DownloadPackage package = ChunkFileTest(64, totalBytes);

        // assert
        Assert.Equal(totalBytes, package.Chunks.Sum(chunk => chunk.Length));
        Assert.True(fileSize >= package.Chunks.Sum(chunk => chunk.Length));
        Assert.Equal(package.Chunks.Last().End, _config.RangeHigh);
    }

    [Fact]
    public void ChunkFileRangeBelowZeroTest()
    {
        // arrange
        _config.RangeLow = -4096;
        _config.RangeHigh = 2048;
        long actualTotalSize = _config.RangeHigh + 1;

        // act
        using DownloadPackage package = ChunkFileTest(64, actualTotalSize);

        // assert
        Assert.Equal(actualTotalSize, package.Chunks.Sum(chunk => chunk.Length));
        Assert.Equal(0, package.Chunks.First().Start);
        Assert.Equal(package.Chunks.Last().End, _config.RangeHigh);
    }

    [Fact]
    public void ChunkFileZeroSizeTest()
    {
        // act
        using DownloadPackage package = ChunkFileTest(64, 0);

        // assert
        Assert.Single(package.Chunks);
        Assert.Equal(0, package.Chunks[0].Start);
        Assert.Equal(-1, package.Chunks[0].End);
        Assert.Equal(0, package.Chunks[0].Length);
    }

    [Theory]
    [InlineData(8, 1024)]
    [InlineData(256, 1024)]
    [InlineData(1024, 1024)]
    [InlineData(64, 10679630)]
    public void PositiveChunkFileRangeTest(int chunkCount, long fileSize)
    {
        // act 
        using DownloadPackage package = ChunkFileTest(chunkCount, fileSize);

        // assert
        Assert.Equal(0, package.Chunks[0].Start);
        Assert.Equal(fileSize - 1, package.Chunks.Last().End);

        for (int i = 1; i < package.Chunks.Length; i++)
        {
            Assert.Equal(package.Chunks[i].Start, package.Chunks[i - 1].End + 1);
        }
    }

    private DownloadPackage ChunkFileTest(int chunkCount, long fileSize = 1024)
    {
        // arrange
        DownloadPackage package = new() {
            TotalFileSize = fileSize
        };
        ChunkHub chunkHub = new(_config);

        // act
        _config.ChunkCount = chunkCount;
        chunkHub.SetFileChunks(package);

        return package;
    }
}