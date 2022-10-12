using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ChunkHubTest
    {
        private DownloadConfiguration _config;

        [TestInitialize]
        public void InitialTests()
        {
            _config = new DownloadConfiguration() {
                Timeout = 100,
                MaxTryAgainOnFailover = 100,
                BufferBlockSize = 1024
            };
        }

        [TestMethod]
        public void ChunkFileByNegativePartsTest()
        {
            // act 
            var package = ChunkFileTest(-1);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFileByZeroPartsTest()
        {
            // act 
            var package = ChunkFileTest(0);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositive1PartsTest()
        {
            // act 
            var package = ChunkFileTest(1);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositive8PartsTest()
        {
            // act 
            var package = ChunkFileTest(8);

            // assert
            Assert.AreEqual(8, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositive256PartsTest()
        {
            // act 
            var package = ChunkFileTest(256);

            // assert
            Assert.AreEqual(256, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFileEqualSizePartsTest()
        {
            // act 
            var package = ChunkFileTest(1024);

            // assert
            Assert.AreEqual(1024, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePartsMoreThanSizeTest()
        {
            // act 
            var package = ChunkFileTest(1030, 1024);

            // assert
            Assert.AreEqual(1024, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            // arrange
            int fileSize = 10679630;

            // act 
            var package = ChunkFileTest(64, fileSize);

            // assert
            Assert.AreEqual(64, package.Chunks.Length);
            Assert.AreEqual(fileSize, package.Chunks.Sum(chunk => chunk.Length));
        }

        [TestMethod]
        public void ChunkFileRangeSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            _config.RangeLow = 1024;
            _config.RangeHigh = 9679630;
            long totalBytes = _config.RangeHigh - _config.RangeLow + 1;

            // act
            var package = ChunkFileTest(64, totalBytes);

            // assert
            Assert.AreEqual(totalBytes, package.Chunks.Sum(chunk => chunk.Length));
            Assert.IsTrue(fileSize >= package.Chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(package.Chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileRangeBelowZeroTest()
        {
            // arrange
            _config.RangeLow = -4096;
            _config.RangeHigh = 2048;
            long actualTotalSize = _config.RangeHigh + 1;

            // act
            var package = ChunkFileTest(64, actualTotalSize);

            // assert
            Assert.AreEqual(actualTotalSize, package.Chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(package.Chunks.First().Start, 0);
            Assert.AreEqual(package.Chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileZeroSizeTest()
        {
            // act
            var package = ChunkFileTest(64, 0);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
            Assert.AreEqual(0, package.Chunks[0].Start);
            Assert.AreEqual(-1, package.Chunks[0].End);
            Assert.AreEqual(0, package.Chunks[0].Length);
        }

        [TestMethod]
        public void ChunkFileRangeTest()
        {
            // arrange
            int fileSize = 10679630;

            // act
            var package = ChunkFileTest(64, fileSize);

            // assert
            Assert.AreEqual(0, package.Chunks[0].Start);
            for (int i = 1; i < package.Chunks.Length; i++)
            {
                Assert.AreEqual(package.Chunks[i].Start, package.Chunks[i - 1].End + 1);
            }
            Assert.AreEqual(package.Chunks.Last().End, fileSize - 1);
        }

        private DownloadPackage ChunkFileTest(int chunkCount, long fileSize = 1024)
        {
            // arrange
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            var chunkHub = new ChunkHub(_config);

            // act
            _config.ChunkCount = chunkCount;
            chunkHub.SetFileChunks(package);

            return package;
        }
    }
}
