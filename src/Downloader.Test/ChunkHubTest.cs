using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkHubTest
    {
        readonly ChunkHub _chunkHub = new ChunkHub(100, 100);

        [TestMethod]
        public void ChunkFileByNegativePartsTest()
        {
            // arrange
            var parts = -1;
            var fileSize = 1024;

            // act
            var chunks = _chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileByZeroPartsTest()
        {
            // arrange
            var parts = 0;
            var fileSize = 1024;

            // act
            var chunks = _chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(1, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePositivePartsTest()
        {
            // arrange
            var fileSize = 1024;

            // act
            var chunks1Parts = _chunkHub.ChunkFile(fileSize, 1);
            var chunks8Parts = _chunkHub.ChunkFile(fileSize, 8);
            var chunks256Parts = _chunkHub.ChunkFile(fileSize, 256);

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

            // act
            var chunks = _chunkHub.ChunkFile(fileSize, fileSize);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFilePartsMoreThanSizeTest()
        {
            // arrange
            var fileSize = 1024;

            // act
            var chunks = _chunkHub.ChunkFile(fileSize, fileSize * 2);

            // assert
            Assert.AreEqual(fileSize, chunks.Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            // arrange
            int fileSize = 10679630;
            int parts = 64;

            // act
            Chunk[] chunks = _chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(fileSize, chunks.Sum(chunk => chunk.Length));
        }

        [TestMethod]
        public void ChunkFileRangeTest()
        {
            // arrange
            int fileSize = 10679630;
            int parts = 64;

            // act
            Chunk[] chunks = _chunkHub.ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(0, chunks[0].Start);
            for (int i = 1; i < chunks.Length; i++)
            {
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
            }
            Assert.AreEqual(chunks.Last().End, fileSize - 1);
        }

        [TestMethod]
        public void MergeChunksByMemoryStorageTest()
        {
            // arrange
            var fileSize = 1024;
            var chunkCount = 8;
            var counter = 0;
            var mergedFilename = FileHelper.GetTempFile("");
            Chunk[] chunks = _chunkHub.ChunkFile(fileSize, chunkCount);
            foreach (Chunk chunk in chunks)
            {
                chunk.Storage = new MemoryStorage(chunk.Length);
                var dummyBytes = DummyData.GenerateRandomBytes((int)chunk.Length);
                chunk.Storage.Write(dummyBytes, 0, dummyBytes.Length).Wait();
            }

            // act
            _chunkHub.MergeChunks(chunks, mergedFilename).Wait();

            // assert
            Assert.IsTrue(File.Exists(mergedFilename));
            var mergedData = File.ReadAllBytes(mergedFilename);
            foreach (Chunk chunk in chunks)
            {
                var chunkStream = chunk.Storage.Read();
                for (int i = 0; i < chunkStream.Length; i++)
                {
                    Assert.AreEqual(chunkStream.ReadByte(), mergedData[counter++]);
                }
                chunk.Clear();
            }
        }

        [TestMethod]
        public void MergeChunksByFileStorageTest()
        {
            // arrange
            var fileSize = 1024;
            var chunkCount = 8;
            var counter = 0;
            var mergedFilename = FileHelper.GetTempFile("");
            Chunk[] chunks = _chunkHub.ChunkFile(fileSize, chunkCount);
            foreach (Chunk chunk in chunks)
            {
                chunk.Storage = new FileStorage(Path.GetTempPath());
                var dummyBytes = DummyData.GenerateRandomBytes((int)chunk.Length);
                chunk.Storage.Write(dummyBytes, 0, dummyBytes.Length).Wait();
            }

            // act
            _chunkHub.MergeChunks(chunks, mergedFilename).Wait();

            // assert
            Assert.IsTrue(File.Exists(mergedFilename));
            var mergedData = File.ReadAllBytes(mergedFilename);
            foreach (Chunk chunk in chunks)
            {
                var chunkStream = chunk.Storage.Read();
                for (int i = 0; i < chunkStream.Length; i++)
                {
                    Assert.AreEqual(chunkStream.ReadByte(), mergedData[counter++]);
                }
                chunk.Clear();
            }
        }
    }
}
