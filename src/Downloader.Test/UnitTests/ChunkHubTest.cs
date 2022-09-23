using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                BufferBlockSize = 1024,
                OnTheFlyDownload = true
            };
        }

        [TestMethod]
        public void ChunkFileByNegativePartsTest()
        {
            // arrange
            _config.ChunkCount = -1;
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_config);

            // act
            var chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileByZeroPartsTest()
        {
            // arrange
            _config.ChunkCount = 0;
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_config);

            // act
            var chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositivePartsTest()
        {
            // arrange
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_config);

            // act
            _config.ChunkCount = 1;
            var chunks1Parts = chunkHub.ChunkFile(fileSize);
            _config.ChunkCount = 8;
            var chunks8Parts = chunkHub.ChunkFile(fileSize);
            _config.ChunkCount = 256;
            var chunks256Parts = chunkHub.ChunkFile(fileSize);

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
            _config.ChunkCount = fileSize;
            var chunkHub = new ChunkHub(_config);

            // act
            var chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePartsMoreThanSizeTest()
        {
            // arrange
            var fileSize = 1024;
            _config.ChunkCount = fileSize * 2;
            var chunkHub = new ChunkHub(_config);

            // act
            var chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(fileSize, chunks.Sum(chunk => chunk.Length));
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
            var chunkHub = new ChunkHub(_config);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(totalBytes);

            // assert
            Assert.AreEqual(totalBytes, chunks.Sum(chunk => chunk.Length));
            Assert.IsTrue(fileSize >= chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileRangeBelowZeroTest()
        {
            // arrange
            _config.ChunkCount = 64;
            _config.RangeLow = -4096;
            _config.RangeHigh = 2048;
            long actualTotalSize = _config.RangeHigh+1;
            var chunkHub = new ChunkHub(_config);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(actualTotalSize);

            // assert
            Assert.AreEqual(actualTotalSize, chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.First().Start, 0);
            Assert.AreEqual(chunks.Last().End, _config.RangeHigh);
        }

        [TestMethod]
        public void ChunkFileZeroSizeTest()
        {
            // arrange
            int fileSize = 0;
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(1, chunks.Length);
            Assert.AreEqual(0, chunks[0].Start);
            Assert.AreEqual(-1, chunks[0].End);
            Assert.AreEqual(0, chunks[0].Length);
        }

        [TestMethod]
        public void ChunkFileRangeTest()
        {
            // arrange
            int fileSize = 10679630;
            _config.ChunkCount = 64;
            var chunkHub = new ChunkHub(_config);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize);

            // assert
            Assert.AreEqual(0, chunks[0].Start);
            for (int i = 1; i < chunks.Length; i++)
            {
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
            }
            Assert.AreEqual(chunks.Last().End, fileSize - 1);
        }

        [TestMethod]
        public async Task MergeChunksByMemoryStorageTest()
        {
            await MergeChunksTest(true);
        }

        [TestMethod]
        public async Task MergeChunksByFileStorageTest()
        {
            await MergeChunksTest(false);
        }

        private async Task MergeChunksTest(bool onTheFly)
        {
            // arrange
            var fileSize = 10240;
            _config.ChunkCount = 8;
            var counter = 0;
            _config.OnTheFlyDownload = onTheFly;
            var chunkHub = new ChunkHub(_config);
            Chunk[] chunks = chunkHub.ChunkFile(fileSize);
            List<byte[]> chunksData = new List<byte[]>();
            foreach (Chunk chunk in chunks)
            {
                var dummyBytes = DummyData.GenerateRandomBytes((int)chunk.Length);
                chunksData.Add(dummyBytes);
                await chunk.Storage.WriteAsync(dummyBytes, 0, dummyBytes.Length, new CancellationToken()).ConfigureAwait(false);
            }

            // act
            using MemoryStream destinationStream = new MemoryStream();
            chunkHub.MergeChunks(chunks, destinationStream, new CancellationToken()).Wait();

            // assert
            var mergedData = destinationStream.ToArray();
            foreach (byte[] chunkData in chunksData)
            {
                foreach (var byteOfChunkData in chunkData)
                {
                    Assert.AreEqual(byteOfChunkData, mergedData[counter++]);
                }
            }
        }

        [TestMethod]
        public void MergeChunksCancellationExceptionTest()
        {
            // arrange
            var chunkHub = new ChunkHub(_config);
            _config.ChunkCount = 8;
            Chunk[] chunks = chunkHub.ChunkFile(10240);

            // act
            async Task MergeAct() => await chunkHub.MergeChunks(chunks, new MemoryStream(), CancellationToken.None).ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<OperationCanceledException>(MergeAct);
        }
    }
}
