using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ThrottledStreamTest
    {
        [TestMethod]
        public void TestStreamReadSpeed()
        {
            // arrange
            var size = 1024;
            var bytesPerSecond = 256; // 256 Byte/s
            var expectedTime = (size / bytesPerSecond) * 1000; // 4000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            var buffer = new byte[bytesPerSecond];
            var readSize = 1;
            using Stream stream = new ThrottledStream(new MemoryStream(randomBytes), bytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Seek(0, SeekOrigin.Begin);
            while (readSize > 0)
            {
                readSize = stream.Read(buffer, 0, buffer.Length);
            }
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime);
        }

        [TestMethod]
        public void TestStreamWriteSpeed()
        {
            // arrange
            var size = 1024;
            var bytesPerSecond = 256; // 32 B/s
            var expectedTime = (size / bytesPerSecond) * 1000; // 4000 Milliseconds
            var randomBytes = DummyData.GenerateRandomBytes(size);
            using Stream stream = new ThrottledStream(new MemoryStream(), bytesPerSecond);
            var stopWatcher = Stopwatch.StartNew();

            // act
            stream.Write(randomBytes, 0, randomBytes.Length);
            stopWatcher.Stop();

            // assert
            Assert.IsTrue(stopWatcher.ElapsedMilliseconds >= expectedTime);
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
