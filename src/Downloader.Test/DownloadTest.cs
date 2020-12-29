using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadTest
    {
        [TestMethod]
        public void DownloadPdf150KTest()
        {
            int expectedFileSize = DownloadTestHelper.FileSize150Kb; // real bytes size
            string address = DownloadTestHelper.File150KbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 1,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            int progressCount = config.ChunkCount *
                                (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount /
                                                  config.BufferBlockSize);
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate { Interlocked.Decrement(ref progressCount); };

            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(progressCount <= 0);

            file.Delete();
        }

        [TestMethod]
        public void DownloadJson1KTest()
        {
            bool downloadCompletedSuccessfully = false;
            int expectedFileSize = DownloadTestHelper.FileSize1Kb; // real bytes size
            string address = DownloadTestHelper.File1KbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 16,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            int progressCount = config.ChunkCount *
                                (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount /
                                                  config.BufferBlockSize);
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate {
                Interlocked.Decrement(ref progressCount);
            };

            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled == false && e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);

            using (StreamReader reader = file.OpenText())
            {
                string json = reader.ReadToEnd();
                object obj = JsonConvert.DeserializeObject(json);
                Assert.IsNotNull(obj);
            }

            Assert.IsTrue(downloadCompletedSuccessfully);
            file.Delete();
        }

        [TestMethod]
        public void StopResumeOnTheFlyDownloadTest()
        {
            int stopCount = 0;
            bool downloadCompletedSuccessfully = false;
            int expectedFileSize = DownloadTestHelper.FileSize150Kb; // real bytes size
            string address = DownloadTestHelper.File150KbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            int progressCount = config.ChunkCount *
                                (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount /
                                                  config.BufferBlockSize);
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate { Interlocked.Decrement(ref progressCount); };
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled)
                {
                    Interlocked.Increment(ref stopCount);
                }
                else if (e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            downloader.CancelAfterDownloading(10); // Stopping after start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            downloader.CancelAfterDownloading(10); // Stopping after resume of downloading.
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point.
            Assert.AreEqual(2, stopCount);
            Assert.IsFalse(downloadCompletedSuccessfully);
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point, again.

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(progressCount <= 0);
            Assert.AreEqual(2, stopCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            file.Delete();
        }

        [TestMethod]
        public void StopResumeOnThFileDownloadTest()
        {
            int stopCount = 0;
            bool downloadCompletedSuccessfully = false;
            int expectedFileSize = DownloadTestHelper.FileSize150Kb; // real bytes size
            string address = DownloadTestHelper.File150KbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = false
            };
            int progressCount = config.ChunkCount *
                                (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount /
                                                  config.BufferBlockSize);
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate { Interlocked.Decrement(ref progressCount); };
            downloader.DownloadFileCompleted += (s, e) => {
                if (e.Cancelled)
                {
                    Interlocked.Increment(ref stopCount);
                }
                else if (e.Error == null)
                {
                    downloadCompletedSuccessfully = true;
                }
            };

            downloader.CancelAfterDownloading(10); // Stopping after start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            downloader.CancelAfterDownloading(10); // Stopping after resume of downloading.
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stopped point.
            Assert.AreEqual(2, stopCount);
            Assert.IsFalse(downloadCompletedSuccessfully);
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stopped point, again.

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(progressCount <= 0);
            Assert.AreEqual(2, stopCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            file.Delete();
        }

        [TestMethod]
        public void SpeedLimitTest()
        {
            ConcurrentBag<long> speedPerSecondsHistory = new ConcurrentBag<long>();
            long lastTick = 0L;
            int expectedFileSize = DownloadTestHelper.FileSize10Mb; // real bytes size
            string address = DownloadTestHelper.File10MbUrl;
            FileInfo file = new FileInfo(Path.GetTempFileName());
            DownloadConfiguration config = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true,
                MaximumBytesPerSecond = 1024 * 1024 // 1MB/s
            };
            int progressCount = config.ChunkCount *
                                (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount /
                                                  config.BufferBlockSize);
            DownloadService downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += (s, e) => {
                Interlocked.Decrement(ref progressCount);
                if (Environment.TickCount64 - lastTick >= 1000)
                {
                    speedPerSecondsHistory.Add(e.BytesPerSecondSpeed);
                    lastTick = Environment.TickCount64;
                }
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            long avgSpeed = (long)speedPerSecondsHistory.Average();

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(progressCount <= 0);
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
            string targetFolderPath = Path.Combine(Path.GetTempPath(), "downloader test folder");
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