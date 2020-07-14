using System;
using System.Collections.Concurrent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadTest
    {
        private string File150KbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/file-sample_150kB.pdf";
        private string File1KbUrl { get; } = "https://raw.githubusercontent.com/bezzad/Downloader/master/src/Downloader.Test/Assets/file_example_JSON_1kb.json";
        private int FileSize150Kb { get; } = 142786;
        private int FileSize1Kb { get; } = 20471;

        [TestMethod]
        public void DownloadPdf150KTest()
        {
            var expectedFileSize = FileSize150Kb; // real bytes size
            var address = File150KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 1,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            var progressCount = config.ChunkCount * (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount / config.BufferBlockSize);
            var downloader = new DownloadService(config);

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
            var downloadCompletedSuccessfully = false;
            var expectedFileSize = FileSize1Kb; // real bytes size
            var address = File1KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 16,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            var progressCount = config.ChunkCount * (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount / config.BufferBlockSize);
            var downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate
            {
                Interlocked.Decrement(ref progressCount);
            };

            downloader.DownloadFileCompleted += (s, e) =>
            {
                if (e.Cancelled == false && e.Error == null)
                    downloadCompletedSuccessfully = true;
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);

            using (var reader = file.OpenText())
            {
                var json = reader.ReadToEnd();
                var obj = JsonConvert.DeserializeObject(json);
                Assert.IsNotNull(obj);
            }

            Assert.IsTrue(downloadCompletedSuccessfully);
            file.Delete();
        }

        [TestMethod]
        public void StopResumeOnTheFlyDownloadTest()
        {
            var stopCount = 0;
            var downloadCompletedSuccessfully = false;
            var expectedFileSize = FileSize150Kb; // real bytes size
            var address = File150KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            var progressCount = config.ChunkCount * (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount / config.BufferBlockSize);
            var downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate { Interlocked.Decrement(ref progressCount); };
            downloader.DownloadFileCompleted += (s, e) =>
            {
                if (e.Cancelled)
                    Interlocked.Increment(ref stopCount);
                else if (e.Error == null)
                    downloadCompletedSuccessfully = true;
            };

            StopResumeDownload(downloader, 10); // Stopping after 1 second from the start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            StopResumeDownload(downloader, 10); // Stopping after 2 second from the resume of downloading.
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
            var stopCount = 0;
            var downloadCompletedSuccessfully = false;
            var expectedFileSize = FileSize150Kb; // real bytes size
            var address = File150KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = false
            };
            var progressCount = config.ChunkCount * (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount / config.BufferBlockSize);
            var downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate { Interlocked.Decrement(ref progressCount); };
            downloader.DownloadFileCompleted += (s, e) =>
            {
                if (e.Cancelled)
                    Interlocked.Increment(ref stopCount);
                else if (e.Error == null)
                    downloadCompletedSuccessfully = true;
            };

            StopResumeDownload(downloader, 10); // Stopping after 1 second from the start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            StopResumeDownload(downloader, 10); // Stopping after 2 second from the resume of downloading.
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
            var speedPerSecondsHistory = new ConcurrentBag<long>();
            var lastTick = 0L;
            var expectedFileSize = FileSize150Kb; // real bytes size
            var address = File150KbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true,
                MaximumBytesPerSecond = 20240 // 20KB/s
            };
            var progressCount = config.ChunkCount * (int)Math.Ceiling((double)expectedFileSize / config.ChunkCount / config.BufferBlockSize);
            var downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += (s, e) =>
            {
                Interlocked.Decrement(ref progressCount);
                if (Environment.TickCount64 - lastTick >= 1000)
                {
                    speedPerSecondsHistory.Add(e.BytesPerSecondSpeed);
                    lastTick = Environment.TickCount64;
                }
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            var avgSpeed = (long)speedPerSecondsHistory.Average();

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.IsTrue(progressCount <= 0);
            Assert.IsTrue(avgSpeed <= config.MaximumBytesPerSecond);

            file.Delete();
        }


        private static async void StopResumeDownload(DownloadService ds, int millisecond)
        {
            while (ds.IsBusy == false)
                await Task.Delay(millisecond);

            ds.CancelAsync();
        }
    }
}
