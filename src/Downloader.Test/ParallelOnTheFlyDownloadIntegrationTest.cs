using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ParallelOnTheFlyDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = true,
                OnTheFlyDownload = true,
                BufferBlockSize = 1024,
                ChunkCount = 16,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
