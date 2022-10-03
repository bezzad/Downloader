using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test.IntegrationTests
{
    [TestClass]
    public class SerialDownloadIntegrationTest : DownloadIntegrationTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            Config = new DownloadConfiguration {
                ParallelDownload = false,
                BufferBlockSize = 10240,
                ParallelCount = 4,
                ChunkCount = 4,
                MaxTryAgainOnFailover = 100
            };
        }
    }
}
