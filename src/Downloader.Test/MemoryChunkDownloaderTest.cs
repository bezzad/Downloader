using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryChunkDownloaderTest : MemoryChunkDownloader
    {
        public MemoryChunkDownloaderTest()
            : base(new MemoryChunk(0, 10000), 1024)
        { }

        public MemoryChunkDownloaderTest(MemoryChunk chunk, int blockSize) : base(chunk, blockSize)
        { }

        [TestMethod]
        public void IsDownloadCompletedOnBeginTest()
        {
            // arrange
            MemoryChunk.Position = 0;
            MemoryChunk.Data = new byte[MemoryChunk.Length];

            // act
            var isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenNoDataTest()
        {
            // arrange
            MemoryChunk.Position = unchecked((int)(Chunk.End - Chunk.Start));
            MemoryChunk.Data = null;

            // act
            var isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenDataIsExistTest()
        {
            // arrange
            MemoryChunk.Position = unchecked((int)(Chunk.End - Chunk.Start));
            MemoryChunk.Data = new byte[MemoryChunk.Length];

            // act
            var isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionTest()
        {
            // arrange
            MemoryChunk.Position = 0;
            MemoryChunk.Data = new byte[MemoryChunk.Length];

            // act
            var isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnOverflowTest()
        {
            // arrange
            MemoryChunk.Position = unchecked((int)(Chunk.End - Chunk.Start)) + 1;
            MemoryChunk.Data = new byte[MemoryChunk.Length];

            // act
            var isValidPosition = IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionTestWhenNoData()
        {
            // arrange
            MemoryChunk.Position = 0;
            MemoryChunk.Data = null;

            // act
            var isValidPosition = IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionTestWhenIsDataExist()
        {
            // arrange
            MemoryChunk.Position = 0;
            MemoryChunk.Data = new byte[MemoryChunk.Length];

            // act
            var isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }
    }
}
