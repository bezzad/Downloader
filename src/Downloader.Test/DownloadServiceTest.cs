using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Downloader.Test
{
    using System;

    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        private void ThrowException()
        {
            throw new Exception("Top level exception", new IOException("Mid level exception", new HttpRequestException("End level exception")));
        }

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
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
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
                    var fileData = new byte[chunk.Length];
                    destinationStream.Read(fileData, 0, (int)chunk.Length);
                    for (var i = 0; i < fileData.Length; i++)
                    {
                        Assert.AreEqual(chunk.Data[i], fileData[i]);
                    }
                }
            }
            Package.Options.ClearPackageAfterDownloadCompleted = true;
            ClearTemps();
        }

        [TestMethod]
        public void MergeFileChunksTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
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
            Package.Options.ClearPackageAfterDownloadCompleted = true;
            ClearTemps();
        }

        [TestMethod]
        public void CancelAsyncTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadFileCompleted += (s, e) => Assert.IsTrue(e.Cancelled);
            this.CancelAfterDownloading(10); // Stopping after start of downloading.
            DownloadFileAsync(address, file.FullName).Wait();
            ClearTemps();
            file.Delete();
        }

        [TestMethod]
        public void BadUrl_CompletesWithErrorTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 0,
                OnTheFlyDownload = true
            };

            var didComplete = false;

            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e)
            {
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

        [TestMethod]
        public void GetFileSizeTest()
        {
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, GetFileSize(new Uri(DownloadTestHelper.File1KbUrl), true).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, GetFileSize(new Uri(DownloadTestHelper.File150KbUrl), true).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize1Mb, GetFileSize(new Uri(DownloadTestHelper.File1MbUrl), true).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize8Mb, GetFileSize(new Uri(DownloadTestHelper.File8MbUrl), true).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize10Mb, GetFileSize(new Uri(DownloadTestHelper.File10MbUrl), true).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize100Mb, GetFileSize(new Uri(DownloadTestHelper.File100MbUrl), true).Result);

            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, GetFileSize(new Uri(DownloadTestHelper.File1KbUrl), false).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, GetFileSize(new Uri(DownloadTestHelper.File150KbUrl), false).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize1Mb, GetFileSize(new Uri(DownloadTestHelper.File1MbUrl), false).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize8Mb, GetFileSize(new Uri(DownloadTestHelper.File8MbUrl), false).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize10Mb, GetFileSize(new Uri(DownloadTestHelper.File10MbUrl), false).Result);
            Assert.AreEqual(DownloadTestHelper.FileSize100Mb, GetFileSize(new Uri(DownloadTestHelper.File100MbUrl), false).Result);
        }

        [TestMethod]
        public void HasSourceTest()
        {
            try
            {
                ThrowException();
            }
            catch (Exception exp)
            {
                Assert.IsTrue(HasSource(exp, GetType().Namespace));
                Assert.IsFalse(HasSource(exp, "System.Net.Sockets"));
                Assert.IsFalse(HasSource(exp, "System.Net.Security"));
            }
        }
    }
}
