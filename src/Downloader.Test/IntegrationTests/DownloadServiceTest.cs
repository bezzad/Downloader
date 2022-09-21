using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
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
        public async Task CancelAsyncTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadStarted += (s, e) => CancelAsync();
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

            // assert
            Assert.IsTrue(IsCancelled);
            Assert.IsNotNull(eventArgs);
            Assert.IsTrue(eventArgs.Cancelled);
            Assert.AreEqual(typeof(TaskCanceledException), eventArgs.Error.GetType());
        }

        [TestMethod]
        public async Task CompletesWithErrorWhenBadUrlTest()
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
            await DownloadFileTaskAsync(address, file.FullName).ConfigureAwait(false);

            // assert
            Assert.IsFalse(IsBusy);
            Assert.IsNotNull(onCompletionException);
            Assert.AreEqual(typeof(WebException), onCompletionException.GetType());

            file.Delete();
        }

        [TestMethod]
        public async Task ClearTest()
        {
            // arrange
            CancelAsync();

            // act
            await Clear();

            // assert
            Assert.IsFalse(IsCancelled);
        }

        [TestMethod]
        public async Task TestPackageSituationAfterDispose()
        {
            // arrange
            var sampleDataLength = 1024;
            Package.TotalFileSize = sampleDataLength * 64;
            Package.Chunks = new[] { new Chunk(0, Package.TotalFileSize) };
            Package.Chunks[0].Storage = new MemoryStorage();
            await Package.Chunks[0].Storage.WriteAsync(DummyData.GenerateRandomBytes(sampleDataLength), 0, sampleDataLength, new CancellationToken()).ConfigureAwait(false);
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
        public async Task TestPackageChunksDataAfterDispose()
        {
            // arrange
            var dummyData = DummyData.GenerateOrderedBytes(1024);
            Package.Chunks = new ChunkHub(Options).ChunkFile(1024 * 64, 64);
            foreach (var chunk in Package.Chunks)
            {
                await chunk.Storage.WriteAsync(dummyData, 0, 1024, new CancellationToken()).ConfigureAwait(false);
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
        public async Task CancelPerformanceTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var watch = new Stopwatch();
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();
            DownloadProgressChanged += (s, e) => {
                watch.Start();
                CancelAsync();
            };
            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            await DownloadFileTaskAsync(address).ConfigureAwait(false);
            watch.Stop();

            // assert
            Assert.IsTrue(eventArgs?.Cancelled);
            Assert.IsTrue(watch.ElapsedMilliseconds < 1000);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
        }

        [TestMethod]
        public async Task ResumePerformanceTest()
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
            await DownloadFileTaskAsync(address).ConfigureAwait(false);
            watch.Start();
            await DownloadFileTaskAsync(Package).ConfigureAwait(false);

            // assert
            Assert.IsFalse(eventArgs?.Cancelled);
            Assert.IsTrue(watch.ElapsedMilliseconds < 1000);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
        }

        [TestMethod]
        public async Task PauseResumeTest()
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
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

            // assert
            Assert.IsTrue(paused);
            Assert.IsFalse(cancelled);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
        }

        [TestMethod]
        public async Task CancelAfterPauseTest()
        {
            // arrange
            AsyncCompletedEventArgs eventArgs = null;
            var pauseStateBeforeCancel = false;
            var cancelStateBeforeCancel = false;
            var pauseStateAfterCancel = false;
            var cancelStateAfterCancel = false;
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            DownloadFileCompleted += (s, e) => eventArgs = e;

            // act
            DownloadProgressChanged += (s, e) => {
                Pause();
                cancelStateBeforeCancel = IsCancelled;
                pauseStateBeforeCancel = IsPaused;
                CancelAsync();
                pauseStateAfterCancel = IsPaused;
                cancelStateAfterCancel = IsCancelled;
            };
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

            // assert
            Assert.IsTrue(pauseStateBeforeCancel);
            Assert.IsFalse(cancelStateBeforeCancel);
            Assert.IsFalse(pauseStateAfterCancel);
            Assert.IsTrue(cancelStateAfterCancel);
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
            Assert.AreEqual(8, Options.ChunkCount);
            Assert.IsFalse(Package.IsSaveComplete);
            Assert.IsTrue(eventArgs.Cancelled);
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
                Assert.IsTrue(activeChunks >= 1  && activeChunks <= 4);
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
                Assert.IsTrue(activeChunks >= 1  && activeChunks <= 4);
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
                Assert.IsTrue(activeChunks >= 1  && activeChunks <= 4);
        }

        [TestMethod]
        public void TestPackageDataAfterCompletionWithSuccess()
        {
            // arrange
            Options.ClearPackageOnCompletionWithFailure = false;
            var states = new DownloadServiceEventsState(this);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);

            // act
            DownloadFileTaskAsync(url).Wait();

            // assert
            Assert.AreEqual(url, Package.Address);
            Assert.IsTrue(states.DownloadSuccessfullCompleted);
            Assert.IsTrue(states.DownloadProgressIsCorrect);
            Assert.IsNull(states.DownloadError);
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.IsNull(Package.Chunks);
        }

        [TestMethod]
        public void TestPackageStatusAfterCompletionWithSuccess()
        {
            // arrange
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            var noneStatus = Package.Status;
            var createdStatus = DownloadStatus.None;
            var runningStatus = DownloadStatus.None;
            var pausedStatus = DownloadStatus.None;
            var resumeStatus = DownloadStatus.None;
            var completedStatus = DownloadStatus.None;

            DownloadStarted += (s, e) => createdStatus = Package.Status;
            DownloadProgressChanged += (s, e) => {
                runningStatus = Package.Status;
                if (e.ProgressPercentage > 50 && e.ProgressPercentage < 70)
                {
                    Pause();
                    pausedStatus = Package.Status;
                    Resume();
                    resumeStatus = Package.Status;
                }
            };
            DownloadFileCompleted += (s, e) => completedStatus = Package.Status;

            // act
            DownloadFileTaskAsync(url).Wait();

            // assert
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Completed, Package.Status);
            Assert.AreEqual(DownloadStatus.Running, createdStatus);
            Assert.AreEqual(DownloadStatus.Running, runningStatus);
            Assert.AreEqual(DownloadStatus.Paused, pausedStatus);
            Assert.AreEqual(DownloadStatus.Running, resumeStatus);
            Assert.AreEqual(DownloadStatus.Completed, completedStatus);
        }

        [TestMethod]
        public void TestPackageStatusAfterCancellation()
        {
            // arrange
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            var noneStatus = Package.Status;
            var createdStatus = DownloadStatus.None;
            var runningStatus = DownloadStatus.None;
            var cancelledStatus = DownloadStatus.None;
            var completedStatus = DownloadStatus.None;

            DownloadStarted += (s, e) => createdStatus = Package.Status;
            DownloadProgressChanged += (s, e) => {
                runningStatus = Package.Status;
                if (e.ProgressPercentage > 50 && e.ProgressPercentage < 70)
                {
                    CancelAsync();
                    cancelledStatus = Package.Status;
                }
            };
            DownloadFileCompleted += (s, e) => completedStatus = Package.Status;

            // act
            DownloadFileTaskAsync(url).Wait();

            // assert
            Assert.IsFalse(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Stopped, Package.Status);
            Assert.AreEqual(DownloadStatus.Running, createdStatus);
            Assert.AreEqual(DownloadStatus.Running, runningStatus);
            Assert.AreEqual(DownloadStatus.Stopped, cancelledStatus);
            Assert.AreEqual(DownloadStatus.Stopped, completedStatus);
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task TestResumeDownloadImmedietalyAfterCancellationAsync()
        {
            // arrange
            var completedState = DownloadStatus.None;
            var checkProgress = false;
            var secondStartProgressPercent = -1d;
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            var tcs = new TaskCompletionSource<bool>();
            DownloadFileCompleted += (s, e) => completedState = Package.Status;

            // act
            DownloadProgressChanged += async (s, e) => {
                if (secondStartProgressPercent < 0)
                {
                    if (checkProgress)
                    {
                        checkProgress = false;
                        secondStartProgressPercent = e.ProgressPercentage;
                    }
                    else if (e.ProgressPercentage > 50 && e.ProgressPercentage < 60)
                    {
                        CancelAsync();
                        checkProgress = true;
                        await DownloadFileTaskAsync(Package).ConfigureAwait(false);
                        tcs.SetResult(true);
                    }
                }
            };
            await DownloadFileTaskAsync(url).ConfigureAwait(false);
            tcs.Task.Wait();

            // assert
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Completed, Package.Status);
            Assert.IsTrue(secondStartProgressPercent > 50, $"progress percent is {secondStartProgressPercent}");
        }


        [TestMethod]
        [Timeout(5000)]
        public async Task TestStopDownloadOnClearWhenRunning()
        {
            // arrange
            var completedState = DownloadStatus.None;
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            DownloadFileCompleted += (s, e) => completedState = Package.Status;

            // act
            DownloadProgressChanged += async (s, e) => {
                if (e.ProgressPercentage > 50 && e.ProgressPercentage < 60)
                    await Clear();
            };
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

            // assert
            Assert.IsFalse(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Stopped, completedState);
            Assert.AreEqual(DownloadStatus.Stopped, Package.Status);
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task TestStopDownloadOnClearWhenPaused()
        {
            // arrange
            var completedState = DownloadStatus.None;
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            DownloadFileCompleted += (s, e) => completedState = Package.Status;

            // act
            DownloadProgressChanged += async (s, e) => {
                if (e.ProgressPercentage > 50 && e.ProgressPercentage < 60)
                {
                    Pause();
                    await Clear();
                }
            };
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

            // assert
            Assert.IsFalse(Package.IsSaveComplete);
            Assert.IsFalse(Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Stopped, completedState);
            Assert.AreEqual(DownloadStatus.Stopped, Package.Status);
        }
    }
}