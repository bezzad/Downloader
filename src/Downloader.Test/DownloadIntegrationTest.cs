using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
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
            var downloadTask = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl);
            downloadTask.Wait();
            using var memoryStream = downloadTask.Result;

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(memoryStream);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(100.0, downloader.Package.SaveProgress);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, memoryStream.Length);
            Assert.IsTrue(DownloadTestHelper.File1Kb.AreEqual(memoryStream));
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
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl, Path.GetTempFileName()).Wait();

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsNotNull(downloadedBytes);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloadedBytes.Length);
            Assert.IsTrue(DownloadTestHelper.File1Kb.AreEqual(new MemoryStream(downloadedBytes)));
        }

        [TestMethod]
        public void Download16KbWithoutFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl,
                new DirectoryInfo(DownloadTestHelper.TempDirectory)).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DownloadTestHelper.TempDirectory));
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DownloadTestHelper.File16Kb.AreEqual(File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var progressChangedCount = (int)Math.Ceiling((double)DownloadTestHelper.FileSize16Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();

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
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl, Path.GetTempFileName()).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedStopCount, stopCount);
            Assert.AreEqual(expectedStopCount, cancellationsOccurrenceCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void StopResumeDownloadFromLastPositionTest()
        {
            // arrange
            var expectedStopCount = 2;
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
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DownloadTestHelper.FileSize16Kb <= totalProgressedByteSize);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, totalReceivedBytes);
        }

        [TestMethod]
        public void StopResumeDownloadOverFirstPackagePositionTest()
        {
            // arrange
            var packageCheckPoint = new DownloadPackage() { Address = DownloadTestHelper.File16KbUrl };
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
                    Address = packageCheckPoint.Address, Chunks = packageCheckPoint.Chunks.Clone() as Chunk[]
                };
                // resume download from first stopped point.
                downloader.DownloadFileTaskAsync(firstCheckPointClone).Wait(); 
            }

            // assert
            Assert.IsTrue(downloader.Package.IsSaveComplete);
            Assert.IsFalse(downloader.Package.IsSaving);
            Assert.IsFalse(isSavingStateOnCancel);
            Assert.IsTrue(isSavingStateBeforCancel);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, totalReceivedBytes);
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
                if (canStopDownload && totalDownloadSize > DownloadTestHelper.FileSize16Kb/2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();
            downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, totalDownloadSize);
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
                if (canStopDownload && totalDownloadSize > DownloadTestHelper.FileSize16Kb/2)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                    canStopDownload = false;
                }
            };

            // act
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File16KbUrl).Wait();
            downloader.Package.Chunks[0].Storage.Clear(); // set position to zero
            downloader.DownloadFileTaskAsync(downloader.Package).Wait(); // resume download from stopped point.

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, totalDownloadSize);
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
            downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Wait();

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= Config.MaximumBytesPerSecond, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");
        }

        [TestMethod]
        public void TestSizeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, stream.Length);
        }

        [TestMethod]
        public void TestTypeWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.IsTrue(stream is MemoryStream);
        }

        [TestMethod]
        public void TestContentWhenDownloadOnMemoryStream()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            using var stream = (MemoryStream)downloader.DownloadFileTaskAsync(DownloadTestHelper.File1KbUrl).Result;

            // assert
            Assert.IsTrue(DownloadTestHelper.File1Kb.SequenceEqual(stream.ToArray()));
        }
    }
}