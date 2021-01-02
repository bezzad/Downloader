using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkDownloaderTest : ChunkDownloader
    {
        public ChunkDownloaderTest()
            : base(null, null)
        {
            Configuration = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 32,
                ParallelDownload = true,
                MaxTryAgainOnFailover = 100,
                Timeout = 100,
                OnTheFlyDownload = true
            };
        }

        [TestMethod]
        public void ReadStreamTest()
        {
            // arrange
            var streamSize = 2048;
            Chunk = new Chunk(0, streamSize - 1) {
                Timeout = 100
            };
            CreateChunkStorage();
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(memoryStream.Length, Chunk.Storage.GetLength());
            var chunkStream = Chunk.Storage.Read();
            for (int i = 0; i < streamSize; i++)
            {
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());
            }

            Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsTest()
        {
            // arrange
            var streamSize = 9 * Configuration.BufferBlockSize;
            Chunk = new Chunk(0, streamSize - 1) {
                Timeout = 100
            };
            using var memoryStream = new MemoryStream(new byte[streamSize]);
            var eventCount = 0;
            DownloadProgressChanged += delegate {
                eventCount++;
            };

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(streamSize/Configuration.BufferBlockSize, eventCount);

            Chunk.Clear();
        }
    }
}