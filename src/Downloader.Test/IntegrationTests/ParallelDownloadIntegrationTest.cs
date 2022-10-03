using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.IntegrationTests
{
    [TestClass]
    public class ParallelDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = true,
                BufferBlockSize = 10240,
                ParallelCount = 4,
                ChunkCount = 8,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
