using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ChunkDownloaderOnFileTest : ChunkDownloaderTest
    {
        [TestInitialize]
        public override void InitialTest()
        {
            var path = Path.GetTempFileName();
            Configuration = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 16,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                MinimumSizeOfChunking = 16,
                Timeout = 100,
            };
            Storage = new ConcurrentStream(path, DummyFileHelper.FileSize16Kb);
        }
    }
}
