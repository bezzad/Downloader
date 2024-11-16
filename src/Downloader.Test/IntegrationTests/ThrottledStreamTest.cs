namespace Downloader.Test.IntegrationTests;

public class ThrottledStreamTest
{
    [Theory]
    [InlineData(1, false)] // TestStreamReadSpeed
    [InlineData(1, true)]  // TestStreamReadSpeedAsync
    [InlineData(2, false)] // TestStreamReadByDynamicSpeed
    [InlineData(2, true)]  // TestStreamReadByDynamicSpeedAsync
    public async Task TestReadStreamSpeed(int speedX, bool asAsync)
    {
        // arrange
        var limitationCoefficient = 0.9; // 90% 
        var size = 10240; // 10KB
        var halfSize = size / 2; // 5KB
        var maxBytesPerSecond = 1024; // 1024 Byte/s
        var maxBytesPerSecondForSecondHalf = 1024 * speedX; // 1024 * X Byte/s
        var expectedTimeForFirstHalf = (halfSize / maxBytesPerSecond) * 1000;
        var expectedTimeForSecondHalf = (halfSize / maxBytesPerSecondForSecondHalf) * 1000;
        var totalExpectedTime = (expectedTimeForFirstHalf + expectedTimeForSecondHalf) * limitationCoefficient;
        var bytes = DummyData.GenerateOrderedBytes(size);
        var buffer = new byte[maxBytesPerSecond / 8];
        var readSize = 1;
        var totalReadSize = 0L;
        await using ThrottledStream stream = new ThrottledStream(new MemoryStream(bytes), maxBytesPerSecond);
        var stopWatcher = Stopwatch.StartNew();

        // act
        stream.Seek(0, SeekOrigin.Begin);
        while (readSize > 0)
        {
            readSize = asAsync
                ? await stream.ReadAsync(buffer, 0, buffer.Length, new CancellationToken())
                : stream.Read(buffer, 0, buffer.Length);
            totalReadSize += readSize;

            // increase speed (2X) after downloading half size
            if (totalReadSize > halfSize && maxBytesPerSecond == stream.BandwidthLimit)
            {
                stream.BandwidthLimit = maxBytesPerSecondForSecondHalf;
            }
        }
        stopWatcher.Stop();

        // assert
        Assert.True(stopWatcher.ElapsedMilliseconds >= totalExpectedTime,
            $"expected duration is: {totalExpectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void TestStreamWriteSpeed()
    {
        // arrange
        var size = 1024;
        var bytesPerSecond = 256; // 256 B/s
        var tolerance = 50; // 50 ms
        var expectedTime = size / bytesPerSecond * 1000; // 4000 Milliseconds
        var randomBytes = DummyData.GenerateRandomBytes(size);
        using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
        var stopWatcher = Stopwatch.StartNew();

        // act
        stream.Write(randomBytes, 0, randomBytes.Length);
        stopWatcher.Stop();

        // assert
        Assert.True(stopWatcher.ElapsedMilliseconds + tolerance >= expectedTime,
            $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task TestStreamWriteSpeedAsync()
    {
        // arrange
        var size = 1024;
        var bytesPerSecond = 256; // 256 B/s
        var tolerance = 50; // 50 ms
        var expectedTime = size / bytesPerSecond * 1000; // 4000 Milliseconds
        var randomBytes = DummyData.GenerateRandomBytes(size);
        await using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
        var stopWatcher = Stopwatch.StartNew();

        // act
        await stream.WriteAsync(randomBytes, 0, randomBytes.Length);
        stopWatcher.Stop();

        // assert
        Assert.True(stopWatcher.ElapsedMilliseconds + tolerance >= expectedTime,
            $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void TestNegativeBandwidth()
    {
        // arrange
        int maximumBytesPerSecond = -1;

        // act
        void CreateThrottledStream()
        {
            using var throttledStream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);
        }

        // assert
        Assert.ThrowsAny<ArgumentOutOfRangeException>(CreateThrottledStream);
    }

    [Fact]
    public void TestZeroBandwidth()
    {
        // arrange
        int maximumBytesPerSecond = 0;

        // act 
        using var throttledStream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

        // assert
        Assert.Equal(long.MaxValue, throttledStream.BandwidthLimit);
    }

    [Theory]
    [InlineData(500, 1024)] // TestStreamIntegrityWithSpeedMoreThanSize
    [InlineData(4096, long.MaxValue)] // TestStreamIntegrityWithMaximumSpeed
    [InlineData(247, 77)] // TestStreamIntegrityWithSpeedLessThanSize
    public void TestStreamIntegrity(int streamSize, long maximumBytesPerSecond)
    {
        // arrange
        byte[] data = DummyData.GenerateOrderedBytes(streamSize);
        byte[] copiedData = new byte[streamSize];
        using Stream stream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

        // act
        stream.Write(data, 0, data.Length);
        stream.Seek(0, SeekOrigin.Begin);
        _ = stream.Read(copiedData, 0, copiedData.Length);

        // assert
        Assert.Equal(streamSize, data.Length);
        Assert.Equal(streamSize, copiedData.Length);
        Assert.True(data.SequenceEqual(copiedData));
    }
}
