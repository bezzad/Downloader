using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ParallelOnTheTempDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = true,
                OnTheFlyDownload = false,
                BufferBlockSize = 1024,
                ChunkCount = 16,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
