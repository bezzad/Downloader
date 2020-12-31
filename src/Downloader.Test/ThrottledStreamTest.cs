using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ThrottledStreamTest
    {
        [TestMethod]
        public void TestStreamRead()
        {
            var size = 1024;
            var bytesPerSecond = 256; // 256 B/s
            var randomBytes = DummyData.GenerateRandomBytes(size);
            using Stream src = new ThrottledStream(new MemoryStream(randomBytes), bytesPerSecond);
            src.Seek(0, SeekOrigin.Begin);
            byte[] buf = new byte[bytesPerSecond];
            int read = 1;
            long start = Environment.TickCount64;

            while (read > 0)
            {
                read = src.Read(buf, 0, buf.Length);
            }

            long elapsed = Environment.TickCount64 - start;
            var expectedTime = (size / bytesPerSecond) * 1000;
            Assert.IsTrue(elapsed >= expectedTime);
        }

        [TestMethod]
        public void TestStreamWrite()
        {
            var size = 1024;
            var bytesPerSecond = 256; // 32 B/s
            var randomBytes = DummyData.GenerateRandomBytes(size);
            using Stream tar = new ThrottledStream(new MemoryStream(), bytesPerSecond); 
            tar.Seek(0, SeekOrigin.Begin);
            var start = Environment.TickCount64;

            tar.Write(randomBytes, 0, randomBytes.Length);

            var elapsed = Environment.TickCount64 - start;
            var expectedTime = (size / bytesPerSecond) * 1000;
            Assert.IsTrue(elapsed >= expectedTime);
        }

        [TestMethod]
        public void TestStreamIntegrity()
        {
            using (Stream tar = new ThrottledStream(new MemoryStream(), 100))
            {
                byte[] buf = DummyData.GenerateOrderedBytes(500);
                tar.Write(buf, 0, buf.Length);
                tar.Seek(0, SeekOrigin.Begin);
                byte[] buf2 = new byte[500];
                tar.Read(buf2, 0, buf2.Length);
                Assert.IsTrue(buf.SequenceEqual(buf2));
            }

            using (Stream tar = new ThrottledStream(new MemoryStream()))
            {
                byte[] buf = DummyData.GenerateOrderedBytes(4096);
                tar.Write(buf, 0, buf.Length);
                tar.Seek(0, SeekOrigin.Begin);
                byte[] buf2 = new byte[4096];
                tar.Read(buf2, 0, buf2.Length);
                Assert.IsTrue(buf.SequenceEqual(buf2));
            }

            using (Stream tar = new ThrottledStream(new MemoryStream(), 77))
            {
                byte[] buf = DummyData.GenerateOrderedBytes(247);
                tar.Write(buf, 0, buf.Length);
                tar.Seek(0, SeekOrigin.Begin);
                byte[] buf2 = new byte[247];
                tar.Read(buf2, 0, buf2.Length);
                Assert.IsTrue(buf.SequenceEqual(buf2));
            }
        }

        [TestMethod]
        public void TestNegativeBandwidth()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(()=> new ThrottledStream(new MemoryStream(), -1));
        }

        [TestMethod]
        public void TestZeroBandwidth()
        {
            var throttledStream = new ThrottledStream(new MemoryStream(), 0);
            Assert.AreEqual(long.MaxValue, throttledStream.BandwidthLimit);
        }
    }
}
