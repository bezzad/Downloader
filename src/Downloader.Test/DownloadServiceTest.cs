using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;
using System.IO;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void CancelAsyncTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                OnTheFlyDownload = true
            };
            DownloadFileCompleted += (s, e) => Assert.IsTrue(e.Cancelled);
            this.CancelAfterDownloading(10); // Stopping after start of downloading.
            DownloadFileAsync(address, file.FullName).Wait();
            ClearChunks();
            file.Delete();
        }

        [TestMethod]
        public void BadUrl_CompletesWithErrorTest()
        {
            var address = DownloadTestHelper.File10MbUrl;
            var file = new FileInfo(Path.GetTempFileName());
            Package.Options = new DownloadConfiguration() {
                BufferBlockSize = 1024,
                ChunkCount = 8,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 0,
                OnTheFlyDownload = true
            };

            var didComplete = false;

            DownloadFileCompleted += delegate (object sender, AsyncCompletedEventArgs e) {
                didComplete = true;
                Assert.IsTrue(e.Error != null);
            };

            var didThrow = false;

            try
            {
                DownloadFileAsync(address, file.FullName).Wait();
            }
            catch
            {
                didThrow = true;
                Assert.IsFalse(IsBusy);
            }

            Assert.IsTrue(didThrow);
            Assert.IsTrue(didComplete);

            Clear();
            file.Delete();
        }
    }
}
