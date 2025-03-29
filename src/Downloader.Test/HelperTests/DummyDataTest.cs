namespace Downloader.Test.HelperTests;

public class DummyDataTest
{
    [Fact]
    public void GenerateOrderedBytesTest()
    {
        // arrange
        int size = 1024;
        byte[] bytes = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();

        // act
        byte[] dummyData = DummyData.GenerateOrderedBytes(size);

        // assert
        Assert.Equal(size, dummyData.Length);
        Assert.True(dummyData.SequenceEqual(bytes));
    }

    [Fact]
    public void GenerateOrderedBytesLessThan1Test()
    {
        // arrange
        int size = 0;

        // act
        void Act() => DummyData.GenerateOrderedBytes(size);

        // assert
        Assert.ThrowsAny<ArgumentException>(Act);
    }

    [Fact]
    public void GenerateRandomBytesTest()
    {
        // arrange
        int size = 1024;

        // act
        byte[] dummyData = DummyData.GenerateRandomBytes(size);

        // assert
        Assert.Equal(size, dummyData.Length);
        Assert.Contains(dummyData, i => i > 0);
    }

    [Fact]
    public void GenerateRandomBytesLessThan1Test()
    {
        // arrange
        int size = 0;

        // act
        void Act() => DummyData.GenerateRandomBytes(size);

        // assert
        Assert.ThrowsAny<ArgumentException>(Act);
    }

    [Fact]
    public void GenerateSingleBytesTest()
    {
        // arrange
        int size = 1024;
        byte fillByte = 13;

        // act
        byte[] dummyData = DummyData.GenerateSingleBytes(size, fillByte);

        // assert
        Assert.Equal(size, dummyData.Length);
        Assert.True(dummyData.All(i => i == fillByte));
    }
}
