using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;
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

            downloader.DownloadFileTaskAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.Package.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);

            file.Delete();
        }

        [TestMethod]
        public void DownloadJson1KTest()
        {
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

            downloader.DownloadFileTaskAsync(address, file.FullName).Wait();
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

            file.Delete();
        }
    }
}
