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
            AsyncCompletedEventArgs eventArgs = null;
            string address = DownloadTestHelper.File10MbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadStarted += (s, e) => this.CancelAfterDownloading(10);
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadFileAsync(address, file.FullName).Wait();

            // assert
            Assert.IsTrue(IsCancelled);
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancelled);
            Assert.AreEqual(typeof(OperationCanceledException), eventArgs.Error.GetType());
            
            Clear();
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
            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e) {
                onCompletionException = e.Error;
            };

            // act
            void Act() => DownloadFileAsync(address, file.FullName).Wait();

            // assert
            Assert.ThrowsException<AggregateException>(Act);
            Assert.IsFalse(IsBusy);
            Assert.IsNotNull(onCompletionException);
            Assert.AreEqual(typeof(WebException), onCompletionException.GetType());

            Clear();
            file.Delete();
        }

        [TestMethod]
        public void ClearChunksTest()
        {
            // arrange
            var hub = new ChunkHub(new DownloadConfiguration() { ChunkCount = 32 });
            Package.Chunks = hub.ChunkFile(1024000, 32);

            // act
            Clear();

            // assert
            Assert.IsNull(Package.Chunks);
        }

        [TestMethod]
        public void ClearPackageTest()
        {
            // arrange
            Package.BytesReceived = 1000;
            Package.TotalFileSize = 1024000;
            Package.FileName = "Test";

            // act
            Clear();

            // assert
            Assert.IsNull(Package.FileName);
            Assert.AreEqual(0, Package.BytesReceived);
            Assert.AreEqual(0, Package.TotalFileSize);
        }
    }
}