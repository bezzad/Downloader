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
        double limitationCoefficient = 0.9; // 90% 
        int size = 10240; // 10KB
        int halfSize = size / 2; // 5KB
        int maxBytesPerSecond = 1024; // 1024 Byte/s
        int maxBytesPerSecondForSecondHalf = 1024 * speedX; // 1024 * X Byte/s
        int expectedTimeForFirstHalf = (halfSize / maxBytesPerSecond) * 1000;
        int expectedTimeForSecondHalf = (halfSize / maxBytesPerSecondForSecondHalf) * 1000;
        double totalExpectedTime = (expectedTimeForFirstHalf + expectedTimeForSecondHalf) * limitationCoefficient;
        byte[] bytes = DummyData.GenerateOrderedBytes(size);
        byte[] buffer = new byte[maxBytesPerSecond / 8];
        int readSize = 1;
        long totalReadSize = 0L;
        await using ThrottledStream stream = new(new MemoryStream(bytes), maxBytesPerSecond);
        Stopwatch stopWatcher = Stopwatch.StartNew();

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
        int size = 1024;
        int bytesPerSecond = 256; // 256 B/s
        int tolerance = 50; // 50 ms
        int expectedTime = size / bytesPerSecond * 1000; // 4000 Milliseconds
        byte[] randomBytes = DummyData.GenerateRandomBytes(size);
        using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
        Stopwatch stopWatcher = Stopwatch.StartNew();

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
        int size = 1024;
        int bytesPerSecond = 256; // 256 B/s
        int tolerance = 50; // 50 ms
        int expectedTime = size / bytesPerSecond * 1000; // 4000 Milliseconds
        byte[] randomBytes = DummyData.GenerateRandomBytes(size);
        await using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
        Stopwatch stopWatcher = Stopwatch.StartNew();

        // act
        await stream.WriteAsync(randomBytes.AsMemory());
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
            using ThrottledStream throttledStream = new(new MemoryStream(), maximumBytesPerSecond);
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
        using ThrottledStream throttledStream = new(new MemoryStream(), maximumBytesPerSecond);

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
