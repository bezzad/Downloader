namespace Downloader.Test.HelperTests;

public class AssertHelperTest
{
    [Fact]
    public void TestDoesNotThrowWhenThrowExp()
    {
        void ThrowException() => throw new DivideByZeroException("TEST");

        AssertHelper.DoesNotThrow<ArgumentNullException>(ThrowException);
    }

    [Fact]
    public void TestChunksAreEquals()
    {
        // arrange
        Chunk chunk1 = new() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailure = 1,
            Position = 386,
            Timeout = 1000
        };

        Chunk chunk2 = new() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailure = 1,
            Position = 386,
            Timeout = 1000
        };

        // act
        AssertHelper.AreEquals(chunk1, chunk2);

        // assert
        Assert.NotEqual(chunk1, chunk2);
    }

    [Fact]
    public void TestChunksAreNotEquals()
    {
        // arrange
        Chunk chunk1 = new() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailure = 1,
            Position = 386,
            Timeout = 1000
        };

        Chunk chunk2 = new() {
            Id = "test-id",
            Start = 512,
            End = 1024,
            MaxTryAgainOnFailure = 1,
            Position = 386,
            Timeout = 1000
        };

        // act
        void TestAssertHelper() => AssertHelper.AreEquals(chunk1, chunk2);

        // assert
        Assert.ThrowsAny<Exception>(TestAssertHelper);
        Assert.NotEqual(chunk1, chunk2);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    public void TestGetRandomName(int length)
    {
        // act
        string name = AssertHelper.GetRandomName(length);

        // assert
        Assert.Equal(length, name.Length);
    }
}
