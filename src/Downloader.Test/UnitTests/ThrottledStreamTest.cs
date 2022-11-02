using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ThrottledStreamTest
    {
        [TestMethod]
        public void TestStreamReadSpeed()
        {
            // arrange
            var limitationCoefficient = 0.8; // 80% 
            var size = 10240; // 10KB
            var maxBytesPerSecond = 1024; // 1024 Byte/s
            var expectedTime = size / maxBytesPerSecond * 1000 * limitationCoefficient; // 80% of 10000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[maxBytesPerSecond / 8];
            var readSize = 1;
            using Stream stream = new ThrottledStream(new MemoryStream(randomBytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = stream.Read(buffer, 0, buffer.Length);
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime,
                $"expected duration is: {expectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public async Task TestStreamReadSpeedAsync()
        {
            // arrange
            var limitationCoefficient = 0.8; // 80% 
            var size = 10240; // 10KB
            var maxBytesPerSecond = 1024; // 1024 Byte/s
            var expectedTime = size / maxBytesPerSecond * 1000 * limitationCoefficient; // 80% of 10000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[maxBytesPerSecond / 8];
            var readSize = 1;
            using Stream stream = new ThrottledStream(new MemoryStream(randomBytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = await stream.ReadAsync(buffer, 0, buffer.Length, new CancellationToken()).ConfigureAwait(false);
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime,
                $"expected duration is: {expectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public void TestStreamReadByDynamicSpeed()
        {
            // arrange
            var limitationCoefficient = 0.9; // 90% 
            var size = 10240; // 10KB
            var maxBytesPerSecond = 1024; // 1 KByte/s
            var halfSize = size / 2;
            // 90% of 10000 Milliseconds
            var expectedTime = ((halfSize / maxBytesPerSecond) + (halfSize / (maxBytesPerSecond * 2))) * 1000 * limitationCoefficient;
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[maxBytesPerSecond / 8];
            var readSize = 1;
            var totalReadSize = 0L;
            using ThrottledStream stream = new ThrottledStream(new MemoryStream(randomBytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = stream.Read(buffer, 0, buffer.Length);
                totalReadSize += readSize;

                // increase speed (2X) after downloading half size
                if (totalReadSize > size / 2 && maxBytesPerSecond == stream.BandwidthLimit)
                {
                    stream.BandwidthLimit *= 2;
                }
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime,
                $"expected duration is: {expectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        public async Task TestStreamReadByDynamicSpeedAsync()
        {
            // arrange
            var limitationCoefficient = 0.9; // 90% 
            var size = 10240; // 10KB
            var maxBytesPerSecond = 1024; // 1 KByte/s
            var halfSize = size / 2;
            // 90% of 10000 Milliseconds
            var expectedTime = ((halfSize / maxBytesPerSecond) + (halfSize / (maxBytesPerSecond * 2))) * 1000 * limitationCoefficient;
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[maxBytesPerSecond / 8];
            var readSize = 1;
            var totalReadSize = 0L;
            using ThrottledStream stream = new ThrottledStream(new MemoryStream(randomBytes), maxBytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = await stream.ReadAsync(buffer, 0, buffer.Length, new CancellationToken()).ConfigureAwait(false);
                totalReadSize += readSize;

                // increase speed (2X) after downloading half size
                if (totalReadSize > size / 2 && maxBytesPerSecond == stream.BandwidthLimit)
                {
                    stream.BandwidthLimit *= 2;
                }
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime,
                $"expected duration is: {expectedTime}ms , but actual duration is: {stopWatcher.ElapsedMilliseconds}ms");
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

        private void TestStreamIntegrity(int streamSize, long maximumBytesPerSecond)
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
