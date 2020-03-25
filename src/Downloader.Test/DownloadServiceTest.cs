using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadServiceTest : DownloadService
    {
        [TestMethod]
        public void ChunkFileTest()
        {
            Assert.AreEqual(10, ChunkFile(1000, 10).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 1000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 10000).Length);
            DownloadedChunks.Clear();
            Assert.AreEqual(1000, ChunkFile(1000, 100000).Length);
        }
    }
}
