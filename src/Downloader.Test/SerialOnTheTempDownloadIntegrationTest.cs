using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class SerialOnTheTempDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = false,
                OnTheFlyDownload = false,
                BufferBlockSize = 10240,
                ChunkCount = 4,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
