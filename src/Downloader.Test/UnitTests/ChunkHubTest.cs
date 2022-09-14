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
        private DownloadConfiguration _configuration;

        [TestInitialize]
        public void InitialTests()
        {
            _configuration = new DownloadConfiguration() {
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
            var parts = -1;
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_configuration);

            // act
            var chunks = chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileByZeroPartsTest()
        {
            // arrange
            var parts = 0;
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_configuration);

            // act
            var chunks = chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositivePartsTest()
        {
            // arrange
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_configuration);

            // act
            var chunks1Parts = chunkHub.ChunkFile(fileSize, 1);
            var chunks8Parts = chunkHub.ChunkFile(fileSize, 8);
            var chunks256Parts = chunkHub.ChunkFile(fileSize, 256);

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
            var chunkHub = new ChunkHub(_configuration);

            // act
            var chunks = chunkHub.ChunkFile(fileSize, fileSize);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePartsMoreThanSizeTest()
        {
            // arrange
            var fileSize = 1024;
            var chunkHub = new ChunkHub(_configuration);

            // act
            var chunks = chunkHub.ChunkFile(fileSize, fileSize * 2);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            int parts = 64;
            var chunkHub = new ChunkHub(_configuration);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(fileSize, chunks.Sum(chunk => chunk.Length));
        }

        [TestMethod]
        public void ChunkFileRangeSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            int parts = 64;
            long rangeLow = 1024;
            long rangeHigh = 9679630;
            long totalBytes = rangeHigh - rangeLow + 1;
            var chunkHub = new ChunkHub(_configuration);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(totalBytes, parts, rangeLow);

            // assert
            Assert.AreEqual(totalBytes, chunks.Sum(chunk => chunk.Length));
            Assert.IsTrue(fileSize >= chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.Last().End, rangeHigh);
        }

        [TestMethod]
        public void ChunkFileRangeBelowZeroTest()
        {
            // arrange
            int parts = 64;
            long rangeLow = -4096;
            long rangeHigh = 2048;
            long actualTotalSize = rangeHigh+1;
            var chunkHub = new ChunkHub(_configuration);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(actualTotalSize, parts, rangeLow);

            // assert
            Assert.AreEqual(actualTotalSize, chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.First().Start, 0);
            Assert.AreEqual(chunks.Last().End, rangeHigh);
        }

        [TestMethod]
        public void ChunkFileZeroSizeTest()
        {
            // arrange
            int fileSize = 0;
            int parts = 64;
            var chunkHub = new ChunkHub(_configuration);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize, parts);

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
            int parts = 64;
            var chunkHub = new ChunkHub(_configuration);

            // act
            Chunk[] chunks = chunkHub.ChunkFile(fileSize, parts);

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
            var chunkCount = 8;
            var counter = 0;
            _configuration.OnTheFlyDownload = onTheFly;
            var chunkHub = new ChunkHub(_configuration);
            Chunk[] chunks = chunkHub.ChunkFile(fileSize, chunkCount);
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
            var chunkHub = new ChunkHub(_configuration);
            Chunk[] chunks = chunkHub.ChunkFile(10240, 8);

            // act
            async Task MergeAct() => await chunkHub.MergeChunks(chunks, new MemoryStream(), CancellationToken.None).ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<OperationCanceledException>(MergeAct);
        }
    }
}
