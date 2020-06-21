using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadTest
    {
        [TestMethod]
        public void DownloadPdf150KTest()
        {
            var expectedFileSize = 142786; // real bytes size
            var address = "https://file-examples.com/wp-content/uploads/2017/10/file-sample_150kB.pdf";
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
            Assert.AreEqual(0, progressCount);

            file.Delete();
        }

        [TestMethod]
        public void DownloadJson1KTest()
        {
            var downloadCompletedSuccessfully = false;
            var expectedFileSize = 20471; // real bytes size
            var address = "https://file-examples.com/wp-content/uploads/2017/02/file_example_JSON_1kb.json";
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

            downloader.DownloadFileCompleted += (s,e) =>
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
            var expectedFileSize = 142786; // real bytes size
            var address = "https://file-examples.com/wp-content/uploads/2017/10/file-sample_150kB.pdf";
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

            StopResumeDownload(downloader, 1000); // Stopping after 1 second from the start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            StopResumeDownload(downloader, 2000); // Stopping after 2 second from the resume of downloading.
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point.
            Assert.AreEqual(2, stopCount);
            Assert.IsFalse(downloadCompletedSuccessfully);
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point, again.
            
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);
            Assert.AreEqual(2, stopCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

            file.Delete();
        }

        [TestMethod]
        public void StopResumeOnThFileDownloadTest()
        {
            var stopCount = 0;
            var downloadCompletedSuccessfully = false;
            var expectedFileSize = 142786; // real bytes size
            var address = "https://file-examples.com/wp-content/uploads/2017/10/file-sample_150kB.pdf";
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

            StopResumeDownload(downloader, 1000); // Stopping after 1 second from the start of downloading.
            downloader.DownloadFileAsync(address, file.FullName).Wait(); // wait to download stopped!
            Assert.AreEqual(1, stopCount);
            StopResumeDownload(downloader, 2000); // Stopping after 2 second from the resume of downloading.
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point.
            Assert.AreEqual(2, stopCount);
            Assert.IsFalse(downloadCompletedSuccessfully);
            downloader.DownloadFileAsync(downloader.Package).Wait(); // resume download from stooped point, again.

            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);
            Assert.AreEqual(2, stopCount);
            Assert.IsTrue(downloadCompletedSuccessfully);

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
