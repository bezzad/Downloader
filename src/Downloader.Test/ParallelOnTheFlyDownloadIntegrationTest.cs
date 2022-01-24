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
                BufferBlockSize = 10240,
                ChunkCount = 8,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
