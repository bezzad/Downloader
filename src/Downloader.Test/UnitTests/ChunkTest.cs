namespace Downloader.Test.UnitTests;

public class ChunkTest(ITestOutputHelper output) : BaseTestClass(output)
{
    private readonly byte[] _testData = DummyData.GenerateOrderedBytes(1024);

    [Fact]
    public void ClearTest()
    {
        // arrange
        Chunk chunk = new(0, 1000) { Position = 100, Timeout = 100 };
        chunk.CanTryAgainOnFailure();

        // act
        chunk.Clear();

        // assert
        Assert.Equal(0, chunk.Position);
        Assert.Equal(0, chunk.FailureCount);
    }

    [Fact]
    public void TestCanTryAgainOnFailureWhenMaxIsZero()
    {
        // arrange
        Chunk chunk = new(0, 1000) { Position = 100, Timeout = 100, MaxTryAgainOnFailure = 0 };

        // act
        bool canTryAgainOnFailure = chunk.CanTryAgainOnFailure();

        // assert
        Assert.False(canTryAgainOnFailure);
        Assert.Equal(1, chunk.FailureCount);
    }

    [Fact]
    public void TestCanTryAgainOnFailureWhenMaxIsOne()
    {
        // arrange
        Chunk chunk = new(0, 1) { MaxTryAgainOnFailure = 1 };

        // act
        bool canTryAgainOnFailure = chunk.CanTryAgainOnFailure();

        // assert
        Assert.True(canTryAgainOnFailure);
        Assert.Equal(1, chunk.FailureCount);
    }

    [Fact]
    public void TestClearEffectLessOnTimeout()
    {
        // arrange
        Chunk chunk = new(0, 1000) { Position = 100, Timeout = 1000 };

        // act
        chunk.Clear();

        // assert
        Assert.Equal(1000, chunk.Timeout);
    }

    [Fact]
    public void IsDownloadCompletedOnBeginTest()
    {
        // arrange
        int size = 1024;
        Chunk chunk = new(0, size);

        // act
        bool isDownloadCompleted = chunk.IsDownloadCompleted();

        // assert
        Assert.False(isDownloadCompleted);
    }

    [Fact]
    public void IsDownloadCompletedWhenNoStorageTest()
    {
        // arrange
        int size = 1024;
        Chunk chunk = new(0, size) {
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
        int size = 1024;
        Chunk chunk = new(0, size) { Position = size - 1 };

        // act
        bool isDownloadCompleted = chunk.IsDownloadCompleted();

        // assert
        Assert.False(isDownloadCompleted);
    }

    [Fact]
    public void IsValidPositionWithStorageTest()
    {
        // arrange
        int size = 1024;
        Chunk chunk = new(0, size);

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.True(isValidPosition);
    }

    [Fact]
    public void IsValidPositionOnOverflowTest()
    {
        // arrange
        Chunk chunk = new(0, _testData.Length - 1) {
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
        Chunk chunk = new(0, 1024) {
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
        Chunk chunk = new(0, -1) { Position = 0 };

        // act
        bool isValidPosition = chunk.IsValidPosition();

        // assert
        Assert.True(isValidPosition);
    }

    [Fact]
    public void ChunkSerializationTest()
    {
        // arrange
        Chunk chunk = new(1024, 1024 + _testData.Length) {
            Position = 1,
            Timeout = 1000,
            MaxTryAgainOnFailure = 3000,
        };

        // act
        string serializedChunk = JsonConvert.SerializeObject(chunk);
        Chunk deserializedChunk = JsonConvert.DeserializeObject<Chunk>(serializedChunk);

        // assert
        AssertHelper.AreEquals(chunk, deserializedChunk);

        chunk.Clear();
    }

    [Fact]
    public void TestCanWriteWhenChunkIsNotFull()
    {
        // arrange
        Chunk chunk = new(0, 1000) { Position = 120 };

        // assert
        Assert.True(chunk.CanWrite);
    }
    
    [Fact]
    public void TestCanWriteWhenChunkIsFull()
    {
        // arrange
        Chunk chunk = new(0, 1000) { Position = 1000 };

        // assert
        Assert.False(chunk.CanWrite);
    }
}