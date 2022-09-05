using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Downloader.Test.IntegrationTests
{
    public abstract class DownloadIntegrationTest
    {
        protected DownloadConfiguration Config { get; set; }

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public void Download1KbWithFilenameTest()
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
            var downloadTask = downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb));
            downloadTask.Wait();
            using var memoryStream = downloadTask.Result;

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(memoryStream);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, memoryStream.Length);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.IsTrue(DummyFileHelper.File1Kb.AreEqual(memoryStream));
        }

        [TestMethod]
        public void TestDownloadAndExecuteFileInDownloadCompletedEvent()
        {
            // arrange
            byte[] downloadedBytes = null;
            var downloadCompletedSuccessfully = false;
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    // Execute the downloaded file within completed event
                    // Note: Execute within this event caused to an IOException:
                    // The process cannot access the file '...\Temp\tmp14D3.tmp' because it is being used by another process.)

                    downloadCompletedSuccessfully = true;
                    downloadedBytes = File.ReadAllBytes(downloader.Package.FileName);
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb), Path.GetTempFileName()).Wait();

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(downloadedBytes);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloadedBytes.Length);
            Assert.IsTrue(DummyFileHelper.File1Kb.AreEqual(new MemoryStream(downloadedBytes)));
        }

        [TestMethod]
        public void Download16KbWithoutFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb),
                new DirectoryInfo(DummyFileHelper.TempDirectory)).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DummyFileHelper.TempDirectory));
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DummyFileHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var progressChangedCount = (int)Math.Ceiling((double)DummyFileHelper.FileSize16Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Wait();

            // assert
            // Note: some times received bytes on read stream method was less than block size!
            Assert.IsTrue(progressChangedCount <= progressCounter);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.Package.IsSaving);
        }

        [TestMethod]
        public void StopResumeDownloadTest()
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
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path.GetTempFileName()).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
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
        public void PauseResumeDownloadTest()
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
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb), Path.GetTempFileName()).Wait();
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
        public void StopResumeDownloadFromLastPositionTest()
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
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalProgressedByteSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalReceivedBytes);
        }

        [TestMethod]
        public void StopResumeDownloadOverFirstPackagePositionTest()
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
            downloader.DownloadFileTaskAsync(packageCheckPoint.Address).Wait();
            while (downloader.IsCancelled)
            {
                isSavingStateOnCancel |= downloader.Package.IsSaving;
                var firstCheckPointClone = new DownloadPackage() {
                    Address = packageCheckPoint.Address,
                    Chunks = packageCheckPoint.Chunks.Clone() as Chunk[]
                };
                // resume download from first stopped point.
                downloader.DownloadFileTaskAsync(firstCheckPointClone).Wait();
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
        public void TestTotalReceivedBytesWhenResumeDownload()
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
                if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb/2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Wait();
            downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalDownloadSize);
            Assert.AreEqual(100.0, lastProgressPercentage);
        }

        [TestMethod]
        public void TestTotalReceivedBytesOnResumeDownloadWhenLostDownloadedData()
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
                if (canStopDownload && totalDownloadSize > DummyFileHelper.FileSize16Kb/2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Wait();
            downloader.Package.Chunks[0].Storage.Clear(); // set position to zero
            downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, totalDownloadSize);
            Assert.AreEqual(100.0, lastProgressPercentage);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
        }

        [TestMethod]
        public void SpeedLimitTest()
        {
            // arrange
            double averageSpeed = 0;
            var progressCounter = 0;
            Config.MaximumBytesPerSecond = 256; // 256 Byte/s
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
                progressCounter++;
            };

            // act
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb)).Wait();

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= Config.MaximumBytesPerSecond, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
        }

        [TestMethod]
        public void DynamicSpeedLimitTest()
        {
            // arrange
            double upperTolerance = 1.25; // 25% upper than expected avg speed
            double expectedAverageSpeed = DummyFileHelper.FileSize16Kb/30; // == (256*16 + 512*8 + 1024*4 + 2048*2)/30
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
            downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Wait();
            averageSpeed /= progressCounter;

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= expectedAverageSpeed*upperTolerance,
                $"Avg Speed: {averageSpeed} , Expected Avg Speed Limit: {expectedAverageSpeed*upperTolerance}, " +
                $"Progress Count: {progressCounter}");
        }

        [TestMethod]
        public void TestSizeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb)).Result;

            // assert
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DummyFileHelper.FileSize1Kb, stream.Length);
        }

        [TestMethod]
        public void TestTypeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb)).Result;

            // assert
            Assert.IsTrue(stream is MemoryStream);
        }

        [TestMethod]
        public void TestContentWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Result;

            // assert
            Assert.IsTrue(DummyFileHelper.File16Kb.SequenceEqual(stream.ToArray()));
        }

        [TestMethod]
        public void Download256BytesRangeOf1KbFileTest()
        {
            // arrange
            Config.RangeDownload = true;
            Config.RangeLow = 256;
            Config.RangeHigh = 511;
            var totalSize = Config.RangeHigh - Config.RangeLow + 1;
            var downloader = new DownloadService(Config);

            // act
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb)).Result;
            var bytes = stream.ToArray();

            // assert
            Assert.IsNotNull(stream);
            Assert.AreEqual(totalSize, stream.Length);
            Assert.AreEqual(totalSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < totalSize; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }

        [TestMethod]
        public void DownloadNegetiveRangeOf1KbFileTest()
        {
            // arrange
            Config.RangeDownload = true;
            Config.RangeLow = -256;
            Config.RangeHigh = 255;
            var totalSize = 256;
            var downloader = new DownloadService(Config);

            // act
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize1Kb)).Result;
            var bytes = stream.ToArray();

            // assert
            Assert.IsNotNull(stream);
            Assert.AreEqual(totalSize, stream.Length);
            Assert.AreEqual(totalSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < totalSize; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }

        [TestMethod]
        public void TestDownloadParallelVsHalfOfChunks()
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
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DummyFileHelper.GetFileUrl(DummyFileHelper.FileSize16Kb)).Result;
            var bytes = stream.ToArray();

            // assert
            Assert.IsTrue(maxParallelCountTasks >= actualMaxParallelCountTasks);
            Assert.IsNotNull(stream);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, stream.Length);
            Assert.AreEqual(DummyFileHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            for (int i = 0; i < DummyFileHelper.FileSize16Kb; i++)
                Assert.AreEqual((byte)i, bytes[i]);
        }
    }
}