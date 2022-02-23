using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.IntegrationTests
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
                BufferBlockSize = 10240,
                ChunkCount = 4,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
