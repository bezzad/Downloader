using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            var file = new FileInfo(Path.GetTempFileName());
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File1KbUrl, file.FullName).Wait();

            // assert
            Assert.IsTrue(downloadCompletedSuccessfully);
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, file.Length);
            Assert.IsTrue(DownloadTestHelper.AreEqual(DownloadTestHelper.File1Kb, file.OpenRead()));

            file.Delete();
        }

        [TestMethod]
        public void Download16KbWithoutFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File16KbUrl,
                new DirectoryInfo(DownloadTestHelper.TempDirectory)).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DownloadTestHelper.TempDirectory));
            Assert.AreEqual(DownloadTestHelper.FileSize16Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DownloadTestHelper.AreEqual(DownloadTestHelper.File16Kb, File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var filename = Path.GetTempFileName();
            var progressChangedCount = (int)Math.Ceiling((double)DownloadTestHelper.FileSize16Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File16KbUrl, filename).Wait();

            // assert
            // Note: some times received bytes on read stream method was less than block size!
            Assert.IsTrue(progressChangedCount <= progressCounter);

            File.Delete(filename);
        }

        [TestMethod]
        public void StopResumeDownloadTest()
        {
            // arrange
            var expectedStopCount = 5;
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
            downloader.DownloadFileAsync(DownloadTestHelper.File150KbUrl, Path.GetTempFileName()).Wait();
            while (expectedStopCount > downloadFileExecutionCounter++)
            {
                downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedStopCount, stopCount);
            Assert.AreEqual(expectedStopCount, cancellationsOccurrenceCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void SpeedLimitTest()
        {
            // arrange
            double averageSpeed = 0;
            var progressCounter = 0;
            Config.MaximumBytesPerSecond = 128; // 128 Byte/s
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                averageSpeed = ((averageSpeed * progressCounter) + e.BytesPerSecondSpeed) / (progressCounter + 1);
                progressCounter++;
            };

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File1KbUrl, Path.GetTempFileName()).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(averageSpeed <= Config.MaximumBytesPerSecond, $"Average Speed: {averageSpeed} , Speed Limit: {Config.MaximumBytesPerSecond}");

            File.Delete(downloader.Package.FileName);
        }
    }
}