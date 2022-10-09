using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.IntegrationTests
{
    public abstract class DownloadIntegrationTest
    {
        protected DownloadConfiguration Config { get; set; }
        protected string URL { get; set; } = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb);

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public async Task DownloadWithFilenameTest()
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
            Assert.IsNull(downloader.Package.FileName);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, memoryStream.Length);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(memoryStream));
        }

        [TestMethod]
        public async Task TestDownloadAndExecuteFileInDownloadCompletedEvent()
        {
            // arrange
            var destFilename = Path.GetTempFileName();
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
                    downloadedBytes = File.ReadAllBytes(downloader.Package.FileName);
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(URL, destFilename).ConfigureAwait(false);

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(downloadedBytes);
            Assert.AreEqual(destFilename, downloader.Package.FileName);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloadedBytes.Length);
            Assert.IsTrue(DummyFileHelper.File1Kb.AreEqual(new MemoryStream(downloadedBytes)));

            File.Delete(destFilename);
        }

        [TestMethod]
        public async Task Download16KbWithoutFilenameTest()
        {
            // arrange
            var dir = new DirectoryInfo(DummyFileHelper.TempDirectory);
            var downloader = new DownloadService(Config);

            // act
            await downloader.DownloadFileTaskAsync(URL, dir).ConfigureAwait(false);

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsNotNull(downloader.Package.FileName);
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DummyFileHelper.TempDirectory));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
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
            downloader.DownloadStarted += delegate {
                if (expectedStopCount > stopCount)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
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
            var packageCheckPoint = new DownloadPackage() { Address = DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb) };
            var stopThreshold = 4100;
            var totalReceivedBytes = 0L;
            var downloader = new DownloadService(Config);
            var isSavingStateOnCancel = false;
            var isSavingStateBeforCancel = false;

            downloader.DownloadProgressChanged += (s, e) => {
                totalReceivedBytes += e.ReceivedBytes.Length;
                isSavingStateBeforCancel |= downloader.Package.IsSaving;
                if (e.ReceivedBytesSize > stopThreshold)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    stopThreshold *= 2;

                    // check point of package for once time
                    packageCheckPoint.Chunks ??= downloader.Package.Chunks.Clone() as Chunk[];
                }
            };

            // act
            await downloader.DownloadFileTaskAsync(packageCheckPoint.Address).ConfigureAwait(false);
            while (downloader.IsCancelled)
            {
                isSavingStateOnCancel |= downloader.Package.IsSaving;
                var firstCheckPointClone = new DownloadPackage() {
                    Address = packageCheckPoint.Address,
                    Chunks = packageCheckPoint.Chunks.Clone() as Chunk[]
                };
                // resume download from first stopped point.
                await downloader.DownloadFileTaskAsync(firstCheckPointClone).ConfigureAwait(false);
            }

            // assert
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.Package.IsSaving);
            Assert.IsFalse(isSavingStateOnCancel);
            Assert.IsTrue(isSavingStateBeforCancel);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalReceivedBytes);
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
            downloader.DownloadProgressChanged += (s, e) => {
                totalDownloadSize += e.ReceivedBytes.Length;
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
    }
}