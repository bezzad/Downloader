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
            // arrange
            _config.ChunkCount = -1;
            var fileSize = 1024;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFileByZeroPartsTest()
        {
            // arrange
            _config.ChunkCount = 0;
            var fileSize = 1024;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(1, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositivePartsTest()
        {
            // arrange
            var fileSize = 1024;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            var chunkHub = new ChunkHub(_config);

            // act
            _config.ChunkCount = 1;
            chunkHub.SetFileChunks(package);
            var chunks1Parts = package.Chunks;
            _config.ChunkCount = 8;
            chunkHub.SetFileChunks(package);
            var chunks8Parts = package.Chunks;
            _config.ChunkCount = 256;
            chunkHub.SetFileChunks(package);
            var chunks256Parts = package.Chunks;

            // assert
            Assert.AreEqual(1, chunks1Parts.Length);
            Assert.AreEqual(8, chunks8Parts.Length);
            Assert.AreEqual(256, chunks256Parts.Length);
        }

        [TestMethod]
        public void ChunkFileEqualSizePartsTest()
        {
            // arrange
            var fileSize = 1024;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            _config.ChunkCount = fileSize;
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(fileSize, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePartsMoreThanSizeTest()
        {
            // arrange
            var fileSize = 1024;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            _config.ChunkCount = fileSize * 2;
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(fileSize, package.Chunks.Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(fileSize, package.Chunks.Sum(chunk => chunk.Length));
        }

        [TestMethod]
        public void ChunkFileRangeSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            _config.ChunkCount = 64;
            _config.RangeLow = 1024;
            _config.RangeHigh = 9679630;
            long totalBytes = _config.RangeHigh - _config.RangeLow + 1;
            var package = new DownloadPackage {
                TotalFileSize = totalBytes
            };
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(totalBytes, package.Chunks.Sum(chunk => chunk.Length));
            Assert.IsTrue(fileSize >= package.Chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(package.Chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileRangeBelowZeroTest()
        {
            // arrange
            _config.ChunkCount = 64;
            _config.RangeLow = -4096;
            _config.RangeHigh = 2048;
            long actualTotalSize = _config.RangeHigh + 1;
            var package = new DownloadPackage {
                TotalFileSize = actualTotalSize
            };
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(actualTotalSize, package.Chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(package.Chunks.First().Start, 0);
            Assert.AreEqual(package.Chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileZeroSizeTest()
        {
            // arrange
            int fileSize = 0;
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

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
            var package = new DownloadPackage {
                TotalFileSize = fileSize
            };
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            chunkHub.SetFileChunks(package);

            // assert
            Assert.AreEqual(0, package.Chunks[0].Start);
            for (int i = 1; i < package.Chunks.Length; i++)
            {
                Assert.AreEqual(package.Chunks[i].Start, package.Chunks[i - 1].End + 1);
            }
            Assert.AreEqual(package.Chunks.Last().End, fileSize - 1);
        }
    }
}
