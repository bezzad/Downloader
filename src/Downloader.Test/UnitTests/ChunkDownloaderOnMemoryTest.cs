using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ChunkDownloaderOnMemoryTest : ChunkDownloaderTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Configuration = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 16,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                MinimumSizeOfChunking = 16,
                Timeout = 100,
            };
            Storage = new ConcurrentStream();
        }
    }
}
