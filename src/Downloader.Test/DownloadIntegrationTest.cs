using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
        public void Download150KbWithoutFilenameTest()
        {
            // arrange
            var downloader = new DownloadService(Config);

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File150KbUrl,
                new DirectoryInfo(DownloadTestHelper.TempDirectory)).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.IsTrue(downloader.Package.FileName.StartsWith(DownloadTestHelper.TempDirectory));
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(DownloadTestHelper.AreEqual(DownloadTestHelper.File150Kb, File.OpenRead(downloader.Package.FileName)));

            File.Delete(downloader.Package.FileName);
        }

        [TestMethod]
        public void DownloadProgressChangedTest()
        {
            // arrange
            var downloader = new DownloadService(Config);
            var filename = Path.GetTempFileName();
            var progressChangedCount = (int)Math.Ceiling((double)DownloadTestHelper.FileSize1Kb / Config.BufferBlockSize);
            var progressCounter = 0;
            downloader.DownloadProgressChanged += (s, e) => Interlocked.Increment(ref progressCounter);

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File1KbUrl, filename).Wait();

            // assert
            // Note: some times received bytes on read stream method was less than block size!
            Assert.IsTrue(progressChangedCount <= progressCounter);

            File.Delete(filename);
        }

        [TestMethod]
        public void StopResumeDownloadTest()
        {
            // arrange
            var stopCount = 0;
            var expectedStopCount = 5;
            var downloadCompletedSuccessfully = false;
            var address = DownloadTestHelper.File150KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var downloader = new DownloadService(Config);
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };
            downloader.DownloadStarted += delegate {
                if (expectedStopCount > ++stopCount)
                {
                    // Stopping after start of downloading
                    downloader.CancelAsync();
                }
            };

            // act
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            while (expectedStopCount > stopCount)
            {
                downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stopped point.
            }

            // assert
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, file.Length);
            Assert.AreEqual(expectedStopCount, stopCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            file.Delete();
        }

        [TestMethod]
        public void SpeedLimitTest()
        {
            // arrange
            var speedPerSecondsHistory = new ConcurrentBag<long>();
            var lastTick = 0L;
            Config.MaximumBytesPerSecond = 10 * 1024; // 10 KByte/s
            var downloader = new DownloadService(Config);
            downloader.DownloadProgressChanged += (s, e) => {
                if (Environment.TickCount - lastTick >= 1000)
                {
                    speedPerSecondsHistory.Add(e.BytesPerSecondSpeed);
                    lastTick = Environment.TickCount;
                }
            };

            // act
            downloader.DownloadFileAsync(DownloadTestHelper.File150KbUrl, Path.GetTempFileName()).Wait();

            // assert
            Assert.IsTrue(File.Exists(downloader.Package.FileName));
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            Assert.IsTrue(speedPerSecondsHistory.Average() <= Config.MaximumBytesPerSecond);

            File.Delete(downloader.Package.FileName);
        }
    }
}