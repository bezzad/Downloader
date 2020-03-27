using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void ChunkFileTest()
        {
            Assert.AreEqual(1, ChunkFile(1000, -1).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1, ChunkFile(1000, 0).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1, ChunkFile(1000, 1).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(10, ChunkFile(1000, 10).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 1000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 10000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 100000).Length);
            DownloadedChunks.Clear();
        }

        [TestMethod]
        public void ChunkFileSizeTest()
        {
            var fileSize = 10679630;
            var parts = 64;
            var chunks = ChunkFile(fileSize, parts).OrderBy(c => c.Start).ToArray();
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

        [TestMethod]
        public void MergeChunksTest()
        {
            var address = "https://file-examples.com/wp-content/uploads/2017/02/zip_10MB.zip";
            var file = new FileInfo(Path.GetTempFileName());
            Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in DownloadedChunks.Values.OrderBy(c => c.Start))
                {
                    var fileData = new byte[chunk.Length];
                    destinationStream.Read(fileData, 0, (int)chunk.Length);
                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(chunk.Data[i], fileData[i]);
                    }
                }
            }
        }

    }
}
