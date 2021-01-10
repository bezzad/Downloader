using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.ComponentModel;
using System.IO;
using System.Net;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void CancelAsyncTest()
        {
            // arrange
            string address = DownloadTestHelper.File10MbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };

            // assert
            DownloadFileCompleted += (s, e) => Assert.IsTrue(e.Cancelled);

            // act
            this.CancelAfterDownloading(10); // Stopping after start of downloading.
            DownloadFileAsync(address, file.FullName).Wait();

            ClearChunks();
            file.Delete();
        }

        [TestMethod]
        public void CompletesWithErrorWhenBadUrlTest()
        {
            // arrange
            Exception onCompletionException = null;
            string address = "https://nofile1";
            FileInfo file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 0,
                OnTheFlyDownload = true
            };

            // act
            void Act() => DownloadFileAsync(address, file.FullName).Wait();

            // assert
            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e) {
                onCompletionException = e.Error;
            };
            Assert.ThrowsException<AggregateException>(Act);
            Assert.IsFalse(IsBusy);
            Assert.IsNotNull(onCompletionException);
            Assert.AreEqual(typeof(WebException), onCompletionException.GetType());

            Clear();
            file.Delete();
        }
    }
}