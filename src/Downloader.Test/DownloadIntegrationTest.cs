using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using Downloader.Test.Properties;
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
            ConcurrentBag<long> speedPerSecondsHistory = new ConcurrentBag<long>();
            long lastTick = 0L;
            int expectedFileSize = DownloadTestHelper.FileSize150Kb; // real bytes size
            string address = DownloadTestHelper.File150KbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true,
                MaximumBytesPerSecond = 10*1024 // 10 KByte/s
            };
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += (s, e) => {
                if (Environment.TickCount - lastTick >= 1000)
                {
                    speedPerSecondsHistory.Add(e.BytesPerSecondSpeed);
                    lastTick = Environment.TickCount;
                }
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            long avgSpeed = (long)speedPerSecondsHistory.Average();

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(avgSpeed <= config.MaximumBytesPerSecond);

            if (File.Exists(file.FullName))
            {
                try
                {
                    file.Delete();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        [TestMethod]
        public void DownloadIntoFolderTest()
        {
            string targetFolderPath = Path.Combine(DownloadTestHelper.TempDirectory, "downloader test folder");
            DirectoryInfo targetFolder = new DirectoryInfo(targetFolderPath);

            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 1,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadService downloader = new DownloadService(config);
            downloader.DownloadFileAsync(DownloadTestHelper.File1KbUrl, targetFolder).Wait();
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb, downloader.Package.TotalFileSize);
            downloader.Clear();
            downloader.DownloadFileAsync(DownloadTestHelper.File150KbUrl, targetFolder).Wait();
            Assert.AreEqual(DownloadTestHelper.FileSize150Kb, downloader.Package.TotalFileSize);
            downloader.Clear();

            Assert.IsTrue(targetFolder.Exists);
            FileInfo[] downloadedFiles = targetFolder.GetFiles();
            long totalSize = downloadedFiles.Sum(file => file.Length);
            Assert.AreEqual(DownloadTestHelper.FileSize1Kb + DownloadTestHelper.FileSize150Kb, totalSize);
            Assert.IsTrue(downloadedFiles.Any(file => file.Name == DownloadTestHelper.File1KbName));
            Assert.IsTrue(downloadedFiles.Any(file => file.Name == DownloadTestHelper.File150KbName));

            targetFolder.Delete(true);
        }
    }
}