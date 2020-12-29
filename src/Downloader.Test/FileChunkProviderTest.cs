using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class FileChunkProviderTest : FileChunkProvider
    {
        public FileChunkProviderTest() : base(new DownloadConfiguration {
            BufferBlockSize = 1024,
            ChunkCount = 32,
            ParallelDownload = true,
            MaxTryAgainOnFailover = 100,
            OnTheFlyDownload = false,
            ClearPackageAfterDownloadCompleted = false
        })
        {
        }

        public FileChunkProviderTest(DownloadConfiguration config) : base(config)
        {
        }

        [TestMethod]
        public void MergeChunksTest()
        {
            string address = DownloadTestHelper.File10MbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadService downloader = new DownloadService(Configuration);
            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (FileStream destinationStream =
                new FileStream(downloader.Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (Chunk chunk in downloader.Package.Chunks)
                {
                    FileChunk fileChunk = (FileChunk)chunk;
                    byte[] fileData = new byte[fileChunk.Length];
                    destinationStream.Read(fileData, 0, (int)fileChunk.Length);
                    byte[] data = new byte[fileChunk.Length];

                    using (FileStream reader = File.OpenRead(fileChunk.FileName))
                    {
                        reader.Read(data);
                    }

                    for (int i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(data[i], fileData[i]);
                    }
                }
            }

            // clear chunk files
            foreach (Chunk chunk in downloader.Package.Chunks)
            {
                FileChunk fileChunk = (FileChunk)chunk;
                if (File.Exists(fileChunk.FileName))
                {
                    File.Delete(fileChunk.FileName);
                }
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
            int fileSize = 10679630;
            int parts = 64;

            // act
            Chunk[] chunks = ChunkFile(fileSize, parts);

            // assert
            Assert.AreEqual(0, chunks[0].Start);
            Assert.AreEqual(fileSize, chunks.Sum(chunk => chunk.Length));
            Assert.AreEqual(chunks.Last().End, fileSize - 1);
            for (int i = 1; i < chunks.Length; i++)
            {
                Assert.AreEqual(chunks[i].Start, chunks[i - 1].End + 1);
            }
        }
    }
}