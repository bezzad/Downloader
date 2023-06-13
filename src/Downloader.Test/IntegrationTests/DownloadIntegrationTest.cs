using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.IntegrationTests
{
    public abstract class DownloadIntegrationTest
    {
        protected DownloadConfiguration Config { get; set; }
        protected string URL { get; set; } = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);
        protected string Filename => Path.GetTempFileName();

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public async Task DownloadUrlWithFilenameOnMemoryTest()
        {
            // arrange
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            // act
            using var memoryStream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(memoryStream);
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsNull(downloader.Package.FileName);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, memoryStream.Length);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(memoryStream));
        }

        [TestMethod]
        public async Task DownloadAndReadFileOnDownloadFileCompletedEventTest()
        {
            // arrange
            var destFilename = Filename;
            byte[] downloadedBytes = null;
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    // Execute the downloaded file within completed event
                    // Note: Execute within this event caused to an IOException:
                    // The process cannot access the file '...\Temp\tmp14D3.tmp'
                    // because it is being used by another process.)

                    downloadCompletedSuccessfully = true;
                    downloadedBytes = File.ReadAllBytes(destFilename);
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL, destFilename).ConfigureAwait(false);

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(downloadedBytes);
            Assert.AreEqual(destFilename, downloader.Package.FileName);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloadedBytes.Length);
            Assert.IsTrue(DummyFileHelper.File16Kb.SequenceEqual(downloadedBytes));

            File.Delete(destFilename);
        }

        [TestMethod]
        public async Task Download16KbWithoutFilenameOnDirectoryTest()
        {
            // arrange
            var dir = new DirectoryInfo(DummyFileHelper.TempDirectory);
            var downloader = new DownloadService(Config);
            var filename = Path.Combine(dir.FullName, DummyFileHelper.FileSize16Kb.ToString());
            File.Delete(filename);

            // act
            await downloader.DownloadFileTaskAsync(URL, dir).ConfigureAwait(false);

            // assert
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsNotNull(downloader.Package.FileName);
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DummyFileHelper.TempDirectory));
            Assert.AreEqual(filename, downloader.Package.FileName);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }


        [TestMethod]
        public async Task Download16KbWithFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName()).ConfigureAwait(false);

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsNotNull(downloader.Package.FileName);
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DummyFileHelper.TempDirectory));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public async Task Download16KbOnMemoryTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            var fileBytes = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, fileBytes.Length);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(fileBytes));
        }

        [TestMethod]
        public async Task DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var progressChangedCount = (int)Math.Ceiling((double)DummyFileHelper.FileSize16Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            // Note: some times received bytes on read stream method was less than block size!
            Assert.IsTrue(progressChangedCount <= progressCounter);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.Package.IsSaving);
        }

        [TestMethod]
        public async Task StopResumeDownloadTest()
        {
            // arrange
            var expectedStopCount = 2;
            var stopCount = 0;
            var cancellationsOccurrenceCount = 0;
            var downloadFileExecutionCounter = 0;
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled && e.Error != null)
                {
                    cancellationsOccurrenceCount++;
                }
                else
                {
                    downloadCompletedSuccessfully = true;
                }
            };
            downloader.DownloadStarted += async delegate {
                if (expectedStopCount > stopCount)
                {
                    // Stopping after start of downloading
                    await downloader.CancelTaskAsync().ConfigureAwait(false);
                    stopCount++;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName()).ConfigureAwait(false);
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                // resume download from stopped point.
                await downloader.DownloadFileTaskAsync(downloader.Package).ConfigureAwait(false);
            }
            var stream = File.ReadAllBytes(downloader.Package.FileName);

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedStopCount, stopCount);
            Assert.AreEqual(expectedStopCount, cancellationsOccurrenceCount);
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsTrue(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public async Task PauseResumeDownloadTest()
        {
            // arrange
            var expectedPauseCount = 2;
            var pauseCount = 0;
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error is null)
                    downloadCompletedSuccessfully = true;
            };
            downloader.DownloadProgressChanged += delegate {
                if (expectedPauseCount > pauseCount)
                {
                    // Stopping after start of downloading
                    downloader.Pause();
                    pauseCount++;
                    downloader.Resume();
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL, Path.GetTempFileName()).ConfigureAwait(false);
            var stream = File.ReadAllBytes(downloader.Package.FileName);

            // assert
            Assert.IsFalse(downloader.IsPaused);
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedPauseCount, pauseCount);
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsTrue(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public async Task StopResumeDownloadFromLastPositionTest()
        {
            // arrange
            var expectedStopCount = 1;
            var stopCount = 0;
            var downloadFileExecutionCounter = 0;
            var totalProgressedByteSize = 0L;
            var totalReceivedBytes = 0L;

            var config = (DownloadConfiguration)Config.Clone();
            config.BufferBlockSize = 1024;
            var downloader = new DownloadService(config);
            downloader.DownloadProgressChanged += (s, e) => {
                totalProgressedByteSize += e.ProgressedByteSize;
                totalReceivedBytes += e.ReceivedBytes.Length;
                if (expectedStopCount > stopCount)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    stopCount++;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                // resume download from stopped point.
                await downloader.DownloadFileTaskAsync(downloader.Package).ConfigureAwait(false);
            }

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalProgressedByteSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalReceivedBytes);
        }

        [TestMethod]
        public async Task StopResumeDownloadOverFirstPackagePositionTest()
        {
            // arrange
            var cancellationCount = 4;
            var downloader = new DownloadService(Config);
            var isSavingStateOnCancel = false;
            var isSavingStateBeforCancel = false;

            downloader.DownloadProgressChanged += async (s, e) => {
                isSavingStateBeforCancel |= downloader.Package.IsSaving;
                if (--cancellationCount > 0)
                {
                    // Stopping after start of downloading
                    await downloader.CancelTaskAsync().ConfigureAwait(false);
                }
            };

            // act
            var result = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            // check point of package for once time
            var firstCheckPointPackage = JsonConvert.SerializeObject(downloader.Package);

            while (downloader.IsCancelled)
            {
                isSavingStateOnCancel |= downloader.Package.IsSaving;
                var restoredPackage = JsonConvert.DeserializeObject<DownloadPackage>(firstCheckPointPackage);

                // resume download from first stopped point.
                result = await downloader.DownloadFileTaskAsync(restoredPackage).ConfigureAwait(false);
            }

            // assert
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.Package.IsSaving);
            Assert.IsFalse(isSavingStateOnCancel);
            Assert.IsTrue(isSavingStateBeforCancel);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, result.Length);
        }

        [TestMethod]
        public async Task TestTotalReceivedBytesWhenResumeDownload()
        {
            // arrange
            var canStopDownload = true;
            var totalDownloadSize = 0L;
            var lastProgressPercentage = 0.0;

            var config = (DownloadConfiguration)Config.Clone();
            config.BufferBlockSize = 1024;
            config.ChunkCount = 1;
            var downloader = new DownloadService(config);
            downloader.DownloadProgressChanged += async (s, e) => {
                totalDownloadSize += e.ReceivedBytes.Length;
                lastProgressPercentage = e.ProgressPercentage;
                if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
                {
                    // Stopping after start of downloading
                    await downloader.CancelTaskAsync().ConfigureAwait(false);
                    canStopDownload = false;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            await downloader.DownloadFileTaskAsync(downloader.Package).ConfigureAwait(false); // resume download from stopped point.

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalDownloadSize);
            Assert.AreEqual(100.0, lastProgressPercentage);
        }

        [TestMethod]
        public async Task TestTotalReceivedBytesOnResumeDownloadWhenLostDownloadedData()
        {
            // arrange
            var canStopDownload = true;
            var totalDownloadSize = 0L;
            var lastProgressPercentage = 0.0;

            var config = (DownloadConfiguration)Config.Clone();
            config.BufferBlockSize = 1024;
            config.ChunkCount = 1;
            var downloader = new DownloadService(config);
            downloader.DownloadProgressChanged += (s, e) => {
                totalDownloadSize = e.ReceivedBytesSize;
                lastProgressPercentage = e.ProgressPercentage;
                if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            downloader.Package.Storage.Dispose(); // set position to zero
            await downloader.DownloadFileTaskAsync(downloader.Package).ConfigureAwait(false); // resume download from stopped point.

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalDownloadSize);
            Assert.AreEqual(100.0, lastProgressPercentage);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
        }

        [TestMethod]
        public async Task SpeedLimitTest()
        {
            // arrange
            double averageSpeed = 0;
            var progressCounter = 0;
            Config.BufferBlockSize = 1024;
            Config.MaximumBytesPerSecond = 1024; // 1024 Byte/s
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
                progressCounter++;
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= Config.MaximumBytesPerSecond * 1.5, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
        }

        [TestMethod]
        public async Task DynamicSpeedLimitTest()
        {
            // arrange
            double upperTolerance = 1.5; // 50% upper than expected avg speed
            double expectedAverageSpeed = DummyFileHelper.FileSize16Kb / 30; // == (256*16 + 512*8 + 1024*4 + 2048*2)/30
            double averageSpeed = 0;
            var progressCounter = 0;

            Config.MaximumBytesPerSecond = 256; // 256 Byte/s
            var downloader = new DownloadService(Config);

            downloader.DownloadProgressChanged += (s, e) => {
                averageSpeed += e.BytesPerSecondSpeed;
                progressCounter++;

                var oneSpeedStepSize = 4096; // DummyFileHelper.FileSize16Kb / 4
                var pow = Math.Ceiling((double)e.ReceivedBytesSize / oneSpeedStepSize);
                Config.MaximumBytesPerSecond = 128 * (int)Math.Pow(2, pow); // 256, 512, 1024, 2048
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            averageSpeed /= progressCounter;

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= expectedAverageSpeed * upperTolerance,
                $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed * upperTolerance}, " +
                $"Progress Count: {progressCounter}");
        }

        [TestMethod]
        public async Task TestSizeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, stream.Length);
        }

        [TestMethod]
        public async Task TestTypeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);

            // assert
            Assert.IsTrue(stream is MemoryStream);
        }

        [TestMethod]
        public async Task TestContentWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            var memStream = stream as MemoryStream;

            // assert
            Assert.IsTrue(DummyFileHelper.File16Kb.SequenceEqual(memStream.ToArray()));
        }

        [TestMethod]
        public async Task Download256BytesRangeOfFileTest()
        {
            // arrange
            Config.RangeDownload = true;
            Config.RangeLow = 256;
            Config.RangeHigh = 511;
            var totalSize = Config.RangeHigh - Config.RangeLow + 1;
            var downloader = new DownloadService(Config);

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            var bytes = ((MemoryStream)stream).ToArray();

            // assert
            Assert.IsNotNull(stream);
            Assert.AreEqual(totalSize, stream.Length);
            Assert.AreEqual(totalSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < totalSize; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }

        [TestMethod]
        public async Task DownloadNegetiveRangeOfFileTest()
        {
            // arrange
            Config.RangeDownload = true;
            Config.RangeLow = -256;
            Config.RangeHigh = 255;
            var totalSize = 256;
            var downloader = new DownloadService(Config);

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            var bytes = ((MemoryStream)stream).ToArray();

            // assert
            Assert.IsNotNull(stream);
            Assert.AreEqual(totalSize, stream.Length);
            Assert.AreEqual(totalSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < totalSize; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }

        [TestMethod]
        public async Task TestDownloadParallelVsHalfOfChunks()
        {
            // arrange
            var maxParallelCountTasks = Config.ChunkCount / 2;
            Config.ParallelCount = maxParallelCountTasks;
            var downloader = new DownloadService(Config);
            var actualMaxParallelCountTasks = 0;
            downloader.ChunkDownloadProgressChanged += (s, e) => {
                actualMaxParallelCountTasks = Math.Max(actualMaxParallelCountTasks, e.ActiveChunks);
            };

            // act
            using var stream = await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            var bytes = ((MemoryStream)stream).ToArray();

            // assert
            Assert.IsTrue(maxParallelCountTasks >= actualMaxParallelCountTasks);
            Assert.IsNotNull(stream);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, stream.Length);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < DummyFileHelper.FileSize16Kb; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task TestResumeImmediatelyAfterCanceling()
        {
            // arrange

            var canStopDownload = true;
            var lastProgressPercentage = 0d;
            bool? stopped = null;
            var tcs = new TaskCompletionSource<bool>();
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => stopped ??= e.Cancelled;
            downloader.DownloadProgressChanged += async (s, e) => {
                if (canStopDownload && e.ProgressPercentage > 50)
                {
                    canStopDownload = false;
                    var package = downloader.Package;
                    downloader.CancelAsync();
                    using var stream = await downloader.DownloadFileTaskAsync(package).ConfigureAwait(false); // resume
                    tcs.SetResult(true);
                }
                else if (canStopDownload == false && lastProgressPercentage <= 0)
                {
                    lastProgressPercentage = e.ProgressPercentage;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL).ConfigureAwait(false);
            await tcs.Task.ConfigureAwait(false);

            // assert
            Assert.IsTrue(stopped);
            Assert.IsTrue(lastProgressPercentage > 50);
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.IsCancelled);
        }

        [TestMethod]
        public async Task KeepFileWhenDownloadFailedTest()
        {
            await KeepOrRemoveFileWhenDownloadFailedTest(false);
        }

        [TestMethod]
        public async Task RemoveFileWhenDownloadFailedTest()
        {
            await KeepOrRemoveFileWhenDownloadFailedTest(true);
        }

        private async Task KeepOrRemoveFileWhenDownloadFailedTest(bool clearFileAfterFailure)
        {
            // arrange
            Config.MaxTryAgainOnFailover = 0;
            Config.ClearPackageOnCompletionWithFailure = clearFileAfterFailure;
            var downloadService = new DownloadService(Config);
            var filename = Path.GetTempFileName();
            var url = DummyFileHelper.GetFileWithFailureAfterOffset(DummyFileHelper.FileSize16Kb, DummyFileHelper.FileSize16Kb / 2);

            // act
            await downloadService.DownloadFileTaskAsync(url, filename).ConfigureAwait(false);

            // assert
            Assert.AreEqual(filename, downloadService.Package.FileName);
            Assert.IsFalse(downloadService.Package.IsSaveComplete);
            Assert.IsFalse(downloadService.Package.IsSaving);
            Assert.AreNotEqual(clearFileAfterFailure, File.Exists(filename));
        }

        [TestMethod]
        public async Task TestRetryDownloadAfterTimeout()
        {
            await testRetryDownloadAfterFailure(true);
        }

        [TestMethod]
        public async Task TestRetryDownloadAfterFailure()
        {
            await testRetryDownloadAfterFailure(false);
        }

        private async Task testRetryDownloadAfterFailure(bool timeout)
        {
            // arrange
            Exception error = null;
            var fileSize = DummyFileHelper.FileSize16Kb;
            var failureOffset = fileSize / 2;
            Config.MaxTryAgainOnFailover = 5;
            Config.BufferBlockSize = 1024;
            Config.MinimumSizeOfChunking = 0;
            Config.Timeout = 100;
            Config.ClearPackageOnCompletionWithFailure = false;
            var downloadService = new DownloadService(Config);
            var url = timeout
                ? DummyFileHelper.GetFileWithTimeoutAfterOffset(fileSize, failureOffset)
                : DummyFileHelper.GetFileWithFailureAfterOffset(fileSize, failureOffset);
            downloadService.DownloadFileCompleted += (s, e) => error = e.Error;

            // act
            var stream = await downloadService.DownloadFileTaskAsync(url).ConfigureAwait(false);
            var retryCount = downloadService.Package.Chunks.Sum(chunk => chunk.FailoverCount);

            // assert
            Assert.IsFalse(downloadService.Package.IsSaveComplete);
            Assert.IsFalse(downloadService.Package.IsSaving);
            Assert.AreEqual(DownloadStatus.Failed, downloadService.Package.Status);
            Assert.IsTrue(Config.MaxTryAgainOnFailover <= retryCount);
            Assert.IsNotNull(error);
            Assert.IsInstanceOfType(error, typeof(WebException));
            Assert.AreEqual(failureOffset, stream.Length);

            await stream.DisposeAsync();
        }

        [TestMethod]
        public async Task DownloadMultipleFilesWithOneDownloaderInstanceTest()
        {
            // arrange
            var size1 = 1024 * 8;
            var size2 = 1024 * 16;
            var size3 = 1024 * 32;
            var url1 = DummyFileHelper.GetFileUrl(size1);
            var url2 = DummyFileHelper.GetFileUrl(size2);
            var url3 = DummyFileHelper.GetFileUrl(size3);
            var downloader = new DownloadService(Config);

            // act
            var file1 = await downloader.DownloadFileTaskAsync(url1).ConfigureAwait(false);
            var file2 = await downloader.DownloadFileTaskAsync(url2).ConfigureAwait(false);
            var file3 = await downloader.DownloadFileTaskAsync(url3).ConfigureAwait(false);

            // assert
            Assert.AreEqual(size1, file1.Length);
            Assert.AreEqual(size2, file2.Length);
            Assert.AreEqual(size3, file3.Length);
        }

        [TestMethod]
        public async Task TestStopDownloadWithCancellationToken()
        {
            // arrange
            var downloadProgress = 0d;
            var downloadCancelled = false;
            var cancelltionTokenSource = new CancellationTokenSource();
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => downloadCancelled = e.Cancelled;
            downloader.DownloadProgressChanged += (s, e) => {
                downloadProgress = e.ProgressPercentage;
                if (e.ProgressPercentage > 10)
                {
                    // Stopping after 10% progress of downloading
                    cancelltionTokenSource.Cancel();
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL, cancelltionTokenSource.Token).ConfigureAwait(false);

            // assert
            Assert.IsTrue(downloadCancelled);
            Assert.IsTrue(downloader.IsCancelled);
            Assert.IsTrue(downloader.Status == DownloadStatus.Stopped);
            Assert.IsTrue(downloadProgress > 10);
        }

        [TestMethod]
        public async Task TestResumeDownloadWithAnotherUrl()
        {
            // arrange
            var url1 = DummyFileHelper.GetFileWithNameUrl("file1.dat", DummyFileHelper.FileSize16Kb);
            var url2 = DummyFileHelper.GetFileWithNameUrl("file2.dat", DummyFileHelper.FileSize16Kb);
            var canStopDownload = true;
            var totalDownloadSize = 0L;
            var config = (DownloadConfiguration)Config.Clone();
            config.BufferBlockSize = 1024;
            config.ChunkCount = 4;
            var downloader = new DownloadService(config);
            downloader.DownloadProgressChanged += (s, e) => {
                totalDownloadSize = e.ReceivedBytesSize;
                if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb / 2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(url1).ConfigureAwait(false);
            await downloader.DownloadFileTaskAsync(downloader.Package, url2); // resume download with new url2.

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalDownloadSize);
            Assert.AreEqual(downloader.Package.Storage.Length, DummyFileHelper.FileSize16Kb);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
        }

        [TestMethod]
        public async Task DownloadAFileFrom8UrlsWith8ChunksTest()
        {
            await DownloadAFileFromMultipleUrlsWithMultipleChunksTest(8, 8);
        }

        [TestMethod]
        public async Task DownloadAFileFrom2UrlsWith8ChunksTest()
        {
            await DownloadAFileFromMultipleUrlsWithMultipleChunksTest(2, 8);
        }

        [TestMethod]
        public async Task DownloadAFileFrom8UrlsWith2ChunksTest()
        {
            await DownloadAFileFromMultipleUrlsWithMultipleChunksTest(8, 2);
        }

        public async Task DownloadAFileFromMultipleUrlsWithMultipleChunksTest(int urlsCount, int chunksCount)
        {
            // arrange
            Config.ChunkCount = chunksCount;
            Config.ParallelCount = chunksCount;
            var totalSize = DummyFileHelper.FileSize16Kb;
            var chunkSize = totalSize / Config.ChunkCount;
            var downloader = new DownloadService(Config);
            var urls = Enumerable.Range(1, urlsCount)
                .Select(i => DummyFileHelper.GetFileWithNameUrl("testfile_" + i, totalSize, (byte)i))
                .ToArray();

            // act
            using var stream = await downloader.DownloadFileTaskAsync(urls).ConfigureAwait(false);
            var bytes = ((MemoryStream)stream).ToArray();

            // assert
            Assert.IsNotNull(stream);
            Assert.AreEqual(totalSize, stream.Length);
            Assert.AreEqual(totalSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < totalSize; i++)
            {
                var chunkIndex = (byte)(i / chunkSize);
                var expectedByte = (chunkIndex % urlsCount) + 1;
                Assert.AreEqual(expectedByte, bytes[i]);
            }
        }

    }
}