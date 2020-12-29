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
            Chunk.Position = 0;
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
            Chunk.Position = 0;
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
            Chunk.Position = 0;
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
            Chunk.Position = 0;
            ((MemoryChunk)Chunk).Data = new byte[Chunk.Length];

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }
    }
}