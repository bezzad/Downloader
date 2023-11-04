using Downloader.Test.Helper;
using System;
using Xunit;

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
        var chunk1 = new Chunk() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailover = 1,
            Position = 386,
            Timeout = 1000
        };

        var chunk2 = new Chunk() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailover = 1,
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
        var chunk1 = new Chunk() {
            Id = "test-id",
            Start = 255,
            End = 512,
            MaxTryAgainOnFailover = 1,
            Position = 386,
            Timeout = 1000
        };

        var chunk2 = new Chunk() {
            Id = "test-id",
            Start = 512,
            End = 1024,
            MaxTryAgainOnFailover = 1,
            Position = 386,
            Timeout = 1000
        };

        // act
        void testAssertHelper() => AssertHelper.AreEquals(chunk1, chunk2);

        // assert
        Assert.ThrowsAny<Exception>(testAssertHelper);
        Assert.NotEqual(chunk1, chunk2);
    }
}
