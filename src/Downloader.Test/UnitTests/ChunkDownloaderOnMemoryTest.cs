using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ChunkDownloaderOnMemoryTest : ChunkDownloaderTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            _configuration = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 16,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                Timeout = 100,
                OnTheFlyDownload = true
            };
            _storage = new MemoryStorage();
        }
    }
}
