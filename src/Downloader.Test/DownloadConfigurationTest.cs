using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class DownloadConfigurationTest
    {
        [TestMethod]
        public void MaximumSpeedPerChunkTest()
        {
            // arrange
            var configuration =
                new DownloadConfiguration {
                    MaximumBytesPerSecond = 10240, 
                    ParallelDownload = true, 
                    ChunkCount = 10
                };

            // act
            var maxSpeed = configuration.MaximumSpeedPerChunk();

            // assert
            Assert.AreEqual(configuration.MaximumBytesPerSecond/  configuration.ChunkCount, maxSpeed);
        }
    }
}
