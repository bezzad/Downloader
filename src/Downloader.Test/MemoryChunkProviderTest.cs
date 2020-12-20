using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class MemoryChunkProviderTest : FileChunkProvider
    {
        public MemoryChunkProviderTest() : base(new DownloadConfiguration() {
            BufferBlockSize = 1024,
            ChunkCount = 32,
            ParallelDownload = true,
            MaxTryAgainOnFailover = 100,
            OnTheFlyDownload = true,
            ClearPackageAfterDownloadCompleted = false
        })
        { }

        public MemoryChunkProviderTest(DownloadConfiguration config) : base(config)
        { }

        [TestMethod]
        public void MergeChunksTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var downloader = new DownloadService(Configuration);
            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using var destinationStream = new FileStream(downloader.Package.FileName, FileMode.Open, FileAccess.Read);
            foreach (var chunk in downloader.Package.Chunks)
            {
                var memoryChunk = (MemoryChunk)chunk;
                var fileData = new byte[memoryChunk.Length];
                destinationStream.Read(fileData, 0, (int)memoryChunk.Length);
                for (var i = 0; i < fileData.Length; i++)
                    Assert.AreEqual(memoryChunk.Data[i], fileData[i]);
            }
        }

        [TestMethod]
        public void ChunkFileTest()
        {
            Assert.AreEqual(1, ChunkFile(1000, -1).Length);
            Assert.AreEqual(1, ChunkFile(1000, 0).Length);
            Assert.AreEqual(1, ChunkFile(1000, 1).Length);
            Assert.AreEqual(10, ChunkFile(1000, 10).Length);
            Assert.AreEqual(1000, ChunkFile(1000, 1000).Length);
            Assert.AreEqual(1000, ChunkFile(1000, 10000).Length);
            Assert.AreEqual(1000, ChunkFile(1000, 100000).Length);
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            var fileSize = 10679630;
            var parts = 64;
            var chunks = ChunkFile(fileSize, parts);
            Assert.AreEqual(parts, chunks.Length);
            Assert.AreEqual(0, chunks[0].Start);
            Assert.AreEqual(fileSize, chunks.Last().End + 1);
            long sumOfChunks = chunks[0].Length;
            for (var i = 1; i < chunks.Length; i++)
            {
                sumOfChunks += chunks[i].Length;
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
            }
            Assert.AreEqual(fileSize, sumOfChunks);
            Assert.AreEqual(chunks.Last().End, fileSize - 1);
        }
    }
}
