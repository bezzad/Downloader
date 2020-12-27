using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkProviderTest : FileChunkProvider
    {
        public FileChunkProviderTest() : base(new DownloadConfiguration() {
            BufferBlockSize = 1024,
            ChunkCount = 32,
            ParallelDownload = true,
            MaxTryAgainOnFailover = 100,
            OnTheFlyDownload = false,
            ClearPackageAfterDownloadCompleted = false
        })
        { }

        public FileChunkProviderTest(DownloadConfiguration config) : base(config)
        { }

        [TestMethod]
        public void MergeChunksTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var downloader = new DownloadService(Configuration);
            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(downloader.Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in downloader.Package.Chunks)
                {
                    var fileChunk = (FileChunk)chunk;
                    var fileData = new byte[fileChunk.Length];
                    destinationStream.Read(fileData, 0, (int)fileChunk.Length);
                    var data = new byte[fileChunk.Length];

                    using (var reader = File.OpenRead(fileChunk.FileName))
                        reader.Read(data);

                    for (var i = 0; i < fileData.Length; i++)
                        Assert.AreEqual(data[i], fileData[i]);
                }
            }

            // clear chunk files
            foreach (var chunk in downloader.Package.Chunks)
            {
                var fileChunk = (FileChunk)chunk;
                if (File.Exists(fileChunk.FileName))
                    File.Delete(fileChunk.FileName);
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
            // arrange
            var fileSize = 10679630;
            var parts = 64;

            // act
            var chunks = ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(0, chunks[0].Start);
            Assert.AreEqual(fileSize, chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.Last().End, fileSize - 1);
            for (var i = 1; i < chunks.Length; i++)
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
        }
    }
}
