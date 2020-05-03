using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void ChunkFileTest()
        {
            Assert.AreEqual(1, ChunkFile(1000, -1).Length);
            Clear();
            Assert.AreEqual(1, ChunkFile(1000, 0).Length);
            Clear();
            Assert.AreEqual(1, ChunkFile(1000, 1).Length);
            Clear();
            Assert.AreEqual(10, ChunkFile(1000, 10).Length);
            Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 1000).Length);
            Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 10000).Length);
            Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 100000).Length);
            Clear();
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
            Package.Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            RemoveTempsAfterDownloadCompleted = false;
            DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in Package.Chunks)
                {
                    var fileData = new byte[chunk.Length];
                    destinationStream.Read(fileData, 0, (int)chunk.Length);
                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(chunk.Data[i], fileData[i]);
                    }
                }
            }
            RemoveTempsAfterDownloadCompleted = true;
            RemoveTemps();
        }

        [TestMethod]
        public void MergeFileChunksTest()
        {
            var address = "https://file-examples.com/wp-content/uploads/2017/02/zip_10MB.zip";
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = false
            };
            RemoveTempsAfterDownloadCompleted = false;
            DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in Package.Chunks)
                {
                    var fileData = new byte[chunk.Length];
                    destinationStream.Read(fileData, 0, (int)chunk.Length);
                    chunk.Data = new byte[chunk.Length];

                    using (var reader = File.OpenRead(chunk.FileName))
                        reader.Read(chunk.Data);

                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(chunk.Data[i], fileData[i]);
                    }
                }
            }
            RemoveTempsAfterDownloadCompleted = true;
            RemoveTemps();
        }

        [TestMethod]
        public void CancelAsyncTest()
        {
            var address = "https://file-examples.com/wp-content/uploads/2017/02/zip_10MB.zip";
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e)
            {
                Assert.IsTrue(e.Cancelled);
            };
            Task.Run(async () =>
            {
                await Task.Delay(4000);
                CancelAsync();
            });
            DownloadFileAsync(address, file.FullName).Wait();
            RemoveTemps();
            file.Delete();
        }
    }
}
