using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.IntegrationTests
{
    [TestClass]
    public class ThrottledStreamTest
    {
        [TestMethod]
        public void TestStreamReadSpeed()
        {
            TestReadStreamSpeed(1, false).Wait();
        }

        [TestMethod]
        public async Task TestStreamReadSpeedAsync()
        {
            await TestReadStreamSpeed(1, true);
        }

        [TestMethod]
        public void TestStreamReadByDynamicSpeed()
        {
            TestReadStreamSpeed(2, false).Wait();
        }

        [TestMethod]
        public async Task TestStreamReadByDynamicSpeedAsync()
        {
            await TestReadStreamSpeed(2, true);
        }

        private static async Task TestReadStreamSpeed(int speedX = 1, bool asAsync = false)
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
            using ThrottledStream stream = new (new MemoryStream(bytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = asAsync
                    ? await stream.ReadAsync(buffer, 0, buffer.Length, new CancellationToken()).ConfigureAwait(false)
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
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= totalExpectedTime,
                $"expected duration is: {totalExpectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
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
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds + tolerance >= expectedTime,
                $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public async Task TestStreamWriteSpeedAsync()
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
            await stream.WriteAsync(randomBytes, 0, randomBytes.Length).ConfigureAwait(false);
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds + tolerance >= expectedTime,
                $"actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
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
            Assert.ThrowsException<ArgumentOutOfRangeException>(CreateThrottledStream);
        }

        [TestMethod]
        public void TestZeroBandwidth()
        {
            // arrange
            int maximumBytesPerSecond = 0;

            // act 
            using var throttledStream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

            // assert
            Assert.AreEqual(long.MaxValue, throttledStream.BandwidthLimit);
        }

        [TestMethod]
        public void TestStreamIntegrityWithSpeedMoreThanSize()
        {
            TestStreamIntegrity(500, 1024);
        }

        [TestMethod]
        public void TestStreamIntegrityWithMaximumSpeed()
        {
            TestStreamIntegrity(4096, long.MaxValue);
        }

        [TestMethod]
        public void TestStreamIntegrityWithSpeedLessThanSize()
        {
            TestStreamIntegrity(247, 77);
        }

        private static void TestStreamIntegrity(int streamSize, long maximumBytesPerSecond)
        {
            // arrange
            byte[] data = DummyData.GenerateOrderedBytes(streamSize);
            byte[] copiedData = new byte[streamSize];
            using Stream stream = new ThrottledStream(new MemoryStream(), maximumBytesPerSecond);

            // act
            stream.Write(data, 0, data.Length);
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(copiedData, 0, copiedData.Length);

            // assert
            Assert.AreEqual(streamSize, data.Length);
            Assert.AreEqual(streamSize, copiedData.Length);
            Assert.IsTrue(data.SequenceEqual(copiedData));
        }
    }
}
