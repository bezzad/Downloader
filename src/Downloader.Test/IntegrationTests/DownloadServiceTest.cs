using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Downloader.Test.IntegrationTests
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        private DownloadConfiguration GetDefaultConfig()
        {
            return new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelCount = 4,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
        }

        [TestMethod]
        public void CancelAsyncTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadStarted += (s, e) => CancelAsync();
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.IsTrue(IsCancelled);
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancelled);
            Assert.AreEqual(typeof(TaskCanceledException), eventArgs.Error.GetType());

            Clear();
        }

        [TestMethod]
        public void CompletesWithErrorWhenBadUrlTest()
        {
            // arrange
            Exception onCompletionException = null;
            string address = "https://nofile";
            FileInfo file = new FileInfo(Path.GetTempFileName());
            Options = GetDefaultConfig();
            Options.MaxTryAgainOnFailover = 0;
            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e) {
                onCompletionException = e.Error;
            };

            // act
            DownloadFileTaskAsync(address, file.FullName).Wait();

            // assert
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
            var sampleDataLength = 1024;
            Package.TotalFileSize = sampleDataLength * 64;
            Package.Chunks = new[] { new Chunk(0, Package.TotalFileSize) };
            Package.Chunks[0].Storage = new MemoryStorage();
            Package.Chunks[0].Storage.WriteAsync(DummyData.GenerateRandomBytes(sampleDataLength), 0, sampleDataLength);
            Package.Chunks[0].SetValidPosition();

            // act
            Dispose();

            // assert
            Assert.IsNotNull(Package.Chunks);
            Assert.AreEqual(sampleDataLength, Package.ReceivedBytesSize);
            Assert.AreEqual(sampleDataLength * 64, Package.TotalFileSize);

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
                Assert.IsTrue(dummyData.AreEqual(chunk.Storage.OpenRead()));
            }

            Package.Clear();
        }

        [TestMethod]
        public void CancelPerformanceTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var watch = new Stopwatch();
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadStarted += (s, e) => {
                watch.Start();
                CancelAsync();
            };
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadFileTaskAsync(address).Wait();
            watch.Stop();

            // assert
            Assert.IsTrue(eventArgs?.Cancelled);
            Assert.IsTrue(watch.ElapsedMilliseconds < 1000);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);

            Clear();
        }

        [TestMethod]
        public void ResumePerformanceTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var watch = new Stopwatch();
            var isCancelled = false;
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadFileCompleted += (s, e) => eventArgs = e;
            DownloadProgressChanged += (s, e) => {
                if (isCancelled == false)
                {
                    CancelAsync();
                    isCancelled=true;
                }
                else
                {
                    watch.Stop();
                }
            };

            // act
            DownloadFileTaskAsync(address).Wait();
            watch.Start();
            DownloadFileTaskAsync(Package).Wait();

            // assert
            Assert.IsFalse(eventArgs?.Cancelled);
            Assert.IsTrue(watch.ElapsedMilliseconds < 1000);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);

            Clear();
        }

        [TestMethod]
        public void PauseResumeTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var paused = false;
            var cancelled = false;
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadProgressChanged += (s, e) => {
                Pause();
                cancelled = IsCancelled;
                paused = IsPaused;
                Resume();
            };
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.IsTrue(paused);
            Assert.IsFalse(cancelled);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);

            // clean up
            Clear();
        }

        [TestMethod]
        public void DownloadParallelNotSupportedUrlTest()
        {
            // arrange
            var actualChunksCount = 0;
            AsyncCompletedEventArgs eventArgs = null;
            string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadFileCompleted += (s, e) => eventArgs = e;
            DownloadStarted += (s, e) => {
                actualChunksCount = Package.Chunks.Length;
            };

            // act
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            Assert.IsFalse(eventArgs?.Cancelled);
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsNull(eventArgs?.Error);
            Assert.AreEqual(1, actualChunksCount);

            // clean up
            Clear();
        }

        [TestMethod]
        public void ResumeNotSupportedUrlTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var isCancelled = false;
            var actualChunksCount = 0;
            var progressCount = 0;
            var cancelOnProgressNo = 6;
            var maxProgressPercentage = 0d;
            var address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadFileCompleted += (s, e) => eventArgs = e;
            DownloadProgressChanged += (s, e) => {
                if (cancelOnProgressNo == progressCount++)
                {
                    CancelAsync();
                    isCancelled=true;
                }
                else if (isCancelled)
                {
                    actualChunksCount = Package.Chunks.Length;
                }
                maxProgressPercentage = Math.Max(e.ProgressPercentage, maxProgressPercentage);
            };

            // act
            DownloadFileTaskAsync(address).Wait(); // start the download
            DownloadFileTaskAsync(Package).Wait(); // resume the downlaod after canceling

            // assert
            Assert.IsTrue(isCancelled);
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            Assert.IsFalse(eventArgs?.Cancelled);
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsNull(eventArgs?.Error);
            Assert.AreEqual(1, actualChunksCount);
            Assert.AreEqual(100, maxProgressPercentage);

            // clean up
            Clear();
        }

        [TestMethod]
        public void ActiveChunksTest()
        {
            // arrange
            var allActiveChunksCount = new List<int>(20);
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            // act
            DownloadProgressChanged += (s, e) => {
                allActiveChunksCount.Add(e.ActiveChunks);
            };
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
            Assert.IsTrue(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(1 <= activeChunks && activeChunks <= 4);

            // clean up
            Clear();
        }

        [TestMethod]
        public void ActiveChunksWithRangeNotSupportedUrlTest()
        {
            // arrange
            var allActiveChunksCount = new List<int>(20);
            string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            // act
            DownloadProgressChanged += (s, e) => {
                allActiveChunksCount.Add(e.ActiveChunks);
            };
            DownloadFileTaskAsync(address).Wait();

            // assert
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(1 <= activeChunks && activeChunks <= 4);

            // clean up
            Clear();
        }

        [TestMethod]
        public void ActiveChunksAfterCancelResumeWithNotSupportedUrlTest()
        {
            // arrange
            var allActiveChunksCount = new List<int>(20);
            var isCancelled = false;
            var actualChunksCount = 0;
            var progressCount = 0;
            var cancelOnProgressNo = 6;
            var address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadProgressChanged += (s, e) => {
                allActiveChunksCount.Add(e.ActiveChunks);
                if (cancelOnProgressNo == progressCount++)
                {
                    CancelAsync();
                    isCancelled=true;
                }
                else if (isCancelled)
                {
                    actualChunksCount = Package.Chunks.Length;
                }
            };

            // act
            DownloadFileTaskAsync(address).Wait(); // start the download
            DownloadFileTaskAsync(Package).Wait(); // resume the downlaod after canceling

            // assert
            Assert.IsTrue(isCancelled);
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.AreEqual(1, actualChunksCount);
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(1 <= activeChunks && activeChunks <= 4);

            // clean up
            Clear();
        }
    }
}