namespace Downloader.Test.UnitTests;

public class ChunkTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly byte[] _testData = DummyData.GenerateOrderedBytes(1024);

    [Fact]
    public void ClearTest()
    {
        // arrange
        var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 100 };
        chunk.CanTryAgainOnFailover();

        // act
        chunk.Clear();

        // assert
        Assert.Equal(0, chunk.Position);
        Assert.Equal(0, chunk.FailoverCount);
    }

    [Fact]
    public void TestCanTryAgainOnFailoverWhenMaxIsZero()
    {
        // arrange
        var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 100, MaxTryAgainOnFailover = 0 };

        // act
        var canTryAgainOnFailover = chunk.CanTryAgainOnFailover();

        // assert
        Assert.False(canTryAgainOnFailover);
        Assert.Equal(1, chunk.FailoverCount);
    }

    [Fact]
    public void TestCanTryAgainOnFailoverWhenMaxIsOne()
    {
        // arrange
        var chunk = new Chunk(0, 1) { MaxTryAgainOnFailover = 1 };

        // act
        var canTryAgainOnFailover = chunk.CanTryAgainOnFailover();

        // assert
        Assert.True(canTryAgainOnFailover);
        Assert.Equal(1, chunk.FailoverCount);
    }

    [Fact]
    public void TestClearEffectLessOnTimeout()
    {
        // arrange
        var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 1000 };

        // act
        chunk.Clear();

        // assert
        Assert.Equal(1000, chunk.Timeout);
    }

    [Fact]
    public void IsDownloadCompletedOnBeginTest()
    {
        // arrange
        var size = 1024;
        var chunk = new Chunk(0, size);

        // act
        bool isDownloadCompleted = chunk.IsDownloadCompleted();

        // assert
        Assert.False(isDownloadCompleted);
    }

    [Fact]
    public void IsDownloadCompletedWhenNoStorageTest()
    {
        // arrange
        var size = 1024;
        var chunk = new Chunk(0, size) {
            Position = size - 1
        };

        // act
        bool isDownloadCompleted = chunk.IsDownloadCompleted();

        // assert
        Assert.False(isDownloadCompleted);
    }

    [Fact]
    public void IsDownloadCompletedWhenStorageNoDataTest()
    {
        // arrange
        var size = 1024;
        var chunk = new Chunk(0, size) { Position = size - 1 };

        // act
        bool isDownloadCompleted = chunk.IsDownloadCompleted();

        // assert
        Assert.False(isDownloadCompleted);
    }

    [Fact]
    public void IsValidPositionWithStorageTest()
    {
        // arrange
        var size = 1024;
        var chunk = new Chunk(0, size);

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.True(isValidPosition);
    }

    [Fact]
    public void IsValidPositionOnOverflowTest()
    {
        // arrange
        var chunk = new Chunk(0, _testData.Length - 1) {
            Position = _testData.Length + 1,
        };

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.False(isValidPosition);
    }

    [Fact]
    public void IsValidPositionWhenNoStorageAndZeroPositionTest()
    {
        // arrange
        var chunk = new Chunk(0, 1024) {
            Position = 0
        };

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.True(isValidPosition);
    }

    [Fact]
    public void IsValidPositionOnZeroSizeTest()
    {
        // arrange
        var chunk = new Chunk(0, -1) { Position = 0 };

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.True(isValidPosition);
    }

    [Fact]
    public void ChunkSerializationTest()
    {
        // arrange
        var chunk = new Chunk(1024, 1024 + _testData.Length) {
            Position = 1,
            Timeout = 1000,
            MaxTryAgainOnFailover = 3000,
        };

        // act
        var serializedChunk = JsonConvert.SerializeObject(chunk);
        var deserializedChunk = JsonConvert.DeserializeObject<Chunk>(serializedChunk);

        // assert
        AssertHelper.AreEquals(chunk, deserializedChunk);

        chunk.Clear();
    }

    [Fact]
    public void TestCanWriteWhenChunkIsNotFull()
    {
        // arrange
        var chunk = new Chunk(0, 1000) { Position = 120 };

        // assert
        Assert.True(chunk.CanWrite);
    }
    
    [Fact]
    public void TestCanWriteWhenChunkIsFull()
    {
        // arrange
        var chunk = new Chunk(0, 1000) { Position = 1000 };

        // assert
        Assert.False(chunk.CanWrite);
    }
}