using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryChunkDownloaderTest : MemoryChunkDownloader
    {
        public MemoryChunkDownloaderTest()
            : base(new MemoryChunk(0, 10000), 1024)
        {
        }

        public MemoryChunkDownloaderTest(MemoryChunk chunk, int blockSize)
            : base(chunk, blockSize)
        {
        }


        [TestMethod]
        public void IsDownloadCompletedOnBeginTest()
        {
            // arrange
            Chunk.Clear();
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenNoDataTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Position = unchecked((int)(Chunk.End - Chunk.Start));
            ((MemoryChunk)Chunk).Data = null;

            // act
            bool isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenDataIsExistTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Position = unchecked((int)(Chunk.End - Chunk.Start));
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionTest()
        {
            // arrange
            Chunk.Clear();
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnOverflowTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Position = unchecked((int)(Chunk.End - Chunk.Start)) + 1;
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionTestWhenNoData()
        {
            // arrange
            Chunk.Clear();
            ((MemoryChunk)Chunk).Data = null;

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionTestWhenIsDataExist()
        {
            // arrange
            Chunk.Clear();
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void ReadStreamTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Timeout = 100;
            var streamSize = 2048;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            for (int i = 0; i < streamSize; i++)
            {
                Assert.AreEqual(randomlyBytes[i], ((MemoryChunk)Chunk).Data[i]);
            }

            Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsTest()
        {
            // arrange
            Chunk.Clear();
            Chunk.Timeout = 100;
            var streamSize = 9 * 1024;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);
            var eventCount = 0;
            DownloadProgressChanged += delegate {
                eventCount++;
            };

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(streamSize/BufferBlockSize, eventCount);

            Chunk.Clear();
        }
    }
}