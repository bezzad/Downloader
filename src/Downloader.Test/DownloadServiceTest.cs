using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;

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

        [TestMethod]
        public void MergeChunksTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true,
                ClearPackageAfterDownloadCompleted = false
            };
            DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in Package.Chunks)
                {
                    var memoryChunk = (MemoryChunk)chunk;
                    var fileData = new byte[memoryChunk.Length];
                    destinationStream.Read(fileData, 0, (int)memoryChunk.Length);
                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(memoryChunk.Data[i], fileData[i]);
                    }
                }
            }
            Package.Options.ClearPackageAfterDownloadCompleted = true;
            ClearChunks();
        }

        [TestMethod]
        public void MergeFileChunksTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = false,
                ClearPackageAfterDownloadCompleted = false
            };
            DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);

            using (var destinationStream = new FileStream(Package.FileName, FileMode.Open, FileAccess.Read))
            {
                foreach (var chunk in Package.Chunks)
                {
                    var fileChunk = (FileChunk)chunk;
                    var fileData = new byte[fileChunk.Length];
                    destinationStream.Read(fileData, 0, (int)fileChunk.Length);
                    var data = new byte[fileChunk.Length];

                    using (var reader = File.OpenRead(fileChunk.FileName))
                        reader.Read(data);

                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(data[i], fileData[i]);
                    }
                }
            }
            Package.Options.ClearPackageAfterDownloadCompleted = true;
            ClearChunks();
        }

        [TestMethod]
        public void CancelAsyncTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadFileCompleted += (s, e) => Assert.IsTrue(e.Cancelled);
            this.CancelAfterDownloading(10); // Stopping after start of downloading.
            DownloadFileAsync(address, file.FullName).Wait();
            ClearChunks();
            file.Delete();
        }

        [TestMethod]
        public void BadUrl_CompletesWithErrorTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 0,
                OnTheFlyDownload = true
            };

            var didComplete = false;

            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e) {
                didComplete = true;
                Assert.IsTrue(e.Error != null);
            };

            var didThrow = false;

            try
            {
                DownloadFileAsync(address, file.FullName).Wait();
            }
            catch
            {
                didThrow = true;
                Assert.IsFalse(IsBusy);
            }

            Assert.IsTrue(didThrow);
            Assert.IsTrue(didComplete);

            Clear();
            file.Delete();
        }
       
    }
}
