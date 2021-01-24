using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class SerialOnTheFlyDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = false,
                OnTheFlyDownload = true,
                BufferBlockSize = 1024,
                ChunkCount = 4,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
