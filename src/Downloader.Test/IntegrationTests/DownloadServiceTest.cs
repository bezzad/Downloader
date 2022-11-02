using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.IntegrationTests
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        private string Filename { get; set; }

        [TestCleanup]
        public void Cleanup()
        {
            Package?.Clear();
            Package?.Storage?.Dispose();
            if (!string.IsNullOrWhiteSpace(Filename))
                File.Delete(Filename);
        }

        private DownloadConfiguration GetDefaultConfig()
        {
            return new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelCount = 4,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                MinimumSizeOfChunking = 0
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
        [Timeout(5000)]
        public async Task CompletesWithErrorWhenBadUrlTest()
        {
            // arrange
            Exception onCompletionException = null;
            string address = "https://nofile";
            Filename = Path.GetTempFileName();
            Options = GetDefaultConfig();
            Options.MaxTryAgainOnFailover = 0;
            DownloadFileCompleted += (s, e) => {
                onCompletionException = e.Error;
            };

            // act
            await DownloadFileTaskAsync(address, Filename).ConfigureAwait(false);

            // assert
            Assert.IsFalse(IsBusy);
            Assert.IsNotNull(onCompletionException);
            Assert.AreEqual(typeof(WebException), onCompletionException.GetType());
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
        public void TestPackageSituationAfterDispose()
        {
            // arrange
            var sampleDataLength = 1024;
            var sampleData = DummyData.GenerateRandomBytes(sampleDataLength);
            Package.TotalFileSize = sampleDataLength * 64;
            Options.ChunkCount = 1;
            new ChunkHub(Options).SetFileChunks(Package);
            Package.BuildStorage(false);
            Package.Storage.WriteAsync(0, sampleData, sampleDataLength);
            Package.Storage.Flush();

            // act
            Dispose();

            // assert
            Assert.IsNotNull(Package.Chunks);
            Assert.AreEqual(sampleDataLength, Package.Storage.Length);
            Assert.AreEqual(sampleDataLength * 64, Package.TotalFileSize);
        }

        [TestMethod]
        public async Task TestPackageChunksDataAfterDispose()
        {
            // arrange
            var chunkSize = 1024;
            var dummyData = DummyData.GenerateOrderedBytes(chunkSize);
            Options.ChunkCount = 64;
            Package.TotalFileSize = chunkSize * 64;
            Package.BuildStorage(false);
            new ChunkHub(Options).SetFileChunks(Package);
            for (int i = 0; i < Package.Chunks.Length; i++)
            {
                var chunk = Package.Chunks[i];
                Package.Storage.WriteAsync(chunk.Start, dummyData, chunkSize);
            }

            // act
            Dispose();
            var stream = Package.Storage.OpenRead();

            // assert
            Assert.IsNotNull(Package.Chunks);
            for (int i = 0; i < Package.Chunks.Length; i++)
            {
                var buffer = new byte[chunkSize];
                await stream.ReadAsync(buffer, 0, chunkSize);
                Assert.IsTrue(dummyData.SequenceEqual(buffer));
            }
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
                    isCancelled = true;
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
        public async Task DownloadParallelNotSupportedUrlTest()
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
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

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
        public async Task ResumeNotSupportedUrlTest()
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
                    isCancelled = true;
                }
                else if (isCancelled)
                {
                    actualChunksCount = Package.Chunks.Length;
                }
                maxProgressPercentage = Math.Max(e.ProgressPercentage, maxProgressPercentage);
            };

            // act
            await DownloadFileTaskAsync(address).ConfigureAwait(false); // start the download
            await DownloadFileTaskAsync(Package).ConfigureAwait(false); // resume the downlaod after canceling

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
        public async Task ActiveChunksTest()
        {
            // arrange
            var allActiveChunksCount = new List<int>(20);
            string address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            // act
            DownloadProgressChanged += (s, e) => {
                allActiveChunksCount.Add(e.ActiveChunks);
            };
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

            // assert
            Assert.AreEqual(4, Options.ParallelCount);
            Assert.AreEqual(8, Options.ChunkCount);
            Assert.IsTrue(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(activeChunks >= 1 && activeChunks <= 4);
        }

        [TestMethod]
        public async Task ActiveChunksWithRangeNotSupportedUrlTest()
        {
            // arrange
            var allActiveChunksCount = new List<int>(20);
            string address = DummyFileHelper.GetFileWithNoAcceptRangeUrl("test.dat", DummyFileHelper.FileSize16Kb);
            Options = GetDefaultConfig();

            // act
            DownloadProgressChanged += (s, e) => {
                allActiveChunksCount.Add(e.ActiveChunks);
            };
            await DownloadFileTaskAsync(address).ConfigureAwait(false);

            // assert
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(activeChunks >= 1 && activeChunks <= 4);
        }

        [TestMethod]
        public async Task ActiveChunksAfterCancelResumeWithNotSupportedUrlTest()
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
                    isCancelled = true;
                }
                else if (isCancelled)
                {
                    actualChunksCount = Package.Chunks.Length;
                }
            };

            // act
            await DownloadFileTaskAsync(address).ConfigureAwait(false); // start the download
            await DownloadFileTaskAsync(Package).ConfigureAwait(false); // resume the downlaod after canceling

            // assert
            Assert.IsTrue(isCancelled);
            Assert.IsFalse(Package.IsSupportDownloadInRange);
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.AreEqual(1, actualChunksCount);
            Assert.AreEqual(1, Options.ParallelCount);
            Assert.AreEqual(1, Options.ChunkCount);
            foreach (var activeChunks in allActiveChunksCount)
                Assert.IsTrue(activeChunks >= 1 && activeChunks <= 4);
        }

        [TestMethod]
        public async Task TestPackageDataAfterCompletionWithSuccess()
        {
            // arrange
            Options.ClearPackageOnCompletionWithFailure = false;
            var states = new DownloadServiceEventsState(this);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);

            // act
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

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
        public async Task TestPackageStatusAfterCompletionWithSuccess()
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
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

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
        public async Task TestPackageStatusAfterCancellation()
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
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

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

        [TestMethod]
        public async Task TestMinimumSizeOfChunking()
        {
            // arrange
            Options = GetDefaultConfig();
            Options.MinimumSizeOfChunking = DummyFileHelper.FileSize16Kb;
            var states = new DownloadServiceEventsState(this);
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile16KbName, DummyFileHelper.FileSize16Kb);
            var activeChunks = 0;
            int? chunkCounts = null;
            var progressIds = new Dictionary<string, bool>();
            ChunkDownloadProgressChanged += (s, e) => {
                activeChunks = Math.Max(activeChunks, e.ActiveChunks);
                progressIds[e.ProgressId] = true;
                chunkCounts ??= Package.Chunks.Length;
            };

            // act
            await DownloadFileTaskAsync(url).ConfigureAwait(false);

            // assert
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.AreEqual(1, activeChunks);
            Assert.AreEqual(1, progressIds.Count);
            Assert.AreEqual(1, chunkCounts);
        }

        [TestMethod]
        public async Task TestCreatePathIfNotExist()
        {
            // arrange
            Options = GetDefaultConfig();
            var url = DummyFileHelper.GetFileWithNameUrl(DummyFileHelper.SampleFile1KbName, DummyFileHelper.FileSize1Kb);
            var path = Path.Combine(Path.GetTempPath(), "TestFolder1", "TestFolder2");
            var dir = new DirectoryInfo(path);

            // act
            if (dir.Exists)
                dir.Delete(true);
            await DownloadFileTaskAsync(url, dir).ConfigureAwait(false);

            // assert
            Assert.IsTrue(Package.IsSaveComplete);
            Assert.IsTrue(Package.FileName.StartsWith(dir.FullName));
            Assert.IsTrue(File.Exists(Package.FileName), "FileName: " + Package.FileName);
        }
    }
}