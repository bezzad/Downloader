using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Threading;

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
            var progressCount = 140;
            var config = new DownloadConfiguration()
            {
                BufferBlockSize = 1024,
                ChunkCount = 1,
                ParallelDownload = false,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            var downloader = new DownloadService(config);

            downloader.DownloadProgressChanged += delegate
            {
                Interlocked.Decrement(ref progressCount);
            };

            downloader.DownloadFileAsync(address, file.FullName).Wait();
            Assert.IsTrue(file.Exists);
            Assert.AreEqual(expectedFileSize, downloader.TotalFileSize);
            Assert.AreEqual(expectedFileSize, file.Length);
            Assert.AreEqual(0, progressCount);
        }
    }
}
