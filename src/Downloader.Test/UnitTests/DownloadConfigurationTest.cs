using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace Downloader.Test.UnitTests
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
            var maxSpeed = configuration.MaximumSpeedPerChunk;

            // assert
            Assert.AreEqual(configuration.MaximumBytesPerSecond / configuration.ChunkCount, maxSpeed);
        }

        [TestMethod]
        public void BufferBlockSizeTest()
        {
            // arrange
            var configuration =
                new DownloadConfiguration {
                    MaximumBytesPerSecond = 10240,
                    ParallelDownload = true,
                    ChunkCount = 10
                };

            // act
            configuration.BufferBlockSize = 10240 * 2;

            // assert
            Assert.AreEqual(configuration.BufferBlockSize, configuration.MaximumSpeedPerChunk);
        }

        [TestMethod]
        public void CloneTest()
        {
            // arrange
            var configProperties = typeof(DownloadConfiguration).GetProperties();
            var config = new DownloadConfiguration() {
                MaxTryAgainOnFailover = 100,
                ParallelDownload = true,
                ChunkCount = 1,
                Timeout = 150,
                BufferBlockSize = 2048,
                MaximumBytesPerSecond = 1024,
                RequestConfiguration = new RequestConfiguration(),
                CheckDiskSizeBeforeDownload = false,
                MinimumSizeOfChunking = 1024,
                ClearPackageOnCompletionWithFailure = true,
            };

            // act
            var cloneConfig = config.Clone() as DownloadConfiguration;

            // assert
            foreach (PropertyInfo property in configProperties)
            {
                Assert.AreEqual(property.GetValue(config), property.GetValue(cloneConfig));
            }
        }
    }
}
