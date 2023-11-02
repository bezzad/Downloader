using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Downloader.Test.HelperTests;

[TestClass]
public class AssertHelperTest
{
    [TestMethod]
    public void TestDoesNotThrowWhenThrowExp()
    {
        void ThrowException() => throw new DivideByZeroException("TEST");

        AssertHelper.DoesNotThrow<ArgumentNullException>(ThrowException);
    }

    [TestMethod]
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
        Assert.AreNotEqual(chunk1, chunk2);
    }

    [TestMethod]
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
        Assert.ThrowsException<AssertFailedException>(testAssertHelper);
        Assert.AreNotEqual(chunk1, chunk2);
    }
}
