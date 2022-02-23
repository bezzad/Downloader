using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.IntegrationTests
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
                BufferBlockSize = 10240,
                ChunkCount = 8,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
