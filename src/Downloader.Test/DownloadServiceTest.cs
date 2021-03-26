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
            string address = DownloadTestHelper.File150KbUrl;
            Options = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadStarted += (s, e) => CancelAsync();
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.IsTrue(IsCancelled);
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancelled);
            Assert.AreEqual(typeof(OperationCanceledException), eventArgs.Error.GetType());

            Clear();
        }

        [TestMethod]
        public void CompletesWithErrorWhenBadUrlTest()
        {
            // arrange
            Exception onCompletionException = null;
            string address = "https://nofile1";
            FileInfo file = new FileInfo(Path.GetTempFileName());
            Options = new DownloadConfiguration {
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
            void Act() => DownloadFileTaskAsync(address, file.FullName).Wait();

            // assert
            Assert.ThrowsException<AggregateException>(Act);
            Assert.IsFalse(IsBusy);
            Assert.IsNotNull(onCompletionException);
            Assert.AreEqual(typeof(WebException), onCompletionException.GetType());

            Clear();
            file.Delete();
        }

        [TestMethod]
        public void ClearTest()
        {
            // arrange
            CancelAsync();

            // act
            Clear();

            // assert
            Assert.IsFalse(IsCancelled);
        }

        [TestMethod]
        public void TestPackageSituationAfterDispose()
        {
            // arrange
            Package.FileName = "test url";
            Package.TotalFileSize = 1024 * 64;
            Package.Chunks = new[] { new Chunk() };
            Package.ReceivedBytesSize = 1024;

            // act
            Dispose();

            // assert
            Assert.IsNotNull(Package.Chunks);
            Assert.AreEqual(1024, Package.ReceivedBytesSize);
            Assert.AreEqual(1024 * 64, Package.TotalFileSize);

            Package.Clear();
        }

        [TestMethod]
        public void TestPackageChunksDataAfterDispose()
        {
            // arrange
            var dummyData = DummyData.GenerateOrderedBytes(1024);
            Package.Chunks = new ChunkHub(Options).ChunkFile(1024 * 64, 64);
            foreach (var chunk in Package.Chunks)
            {
                chunk.Storage.WriteAsync(dummyData, 0, 1024).Wait();
            }

            // act
            Dispose();

            // assert
            Assert.IsNotNull(Package.Chunks);
            foreach (var chunk in Package.Chunks)
            {
                Assert.IsTrue(DownloadTestHelper.AreEqual(dummyData, chunk.Storage.OpenRead()));
            }

            Package.Clear();
        }
    }
}