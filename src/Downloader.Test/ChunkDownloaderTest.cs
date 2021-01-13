using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        public void ReadStreamWhenFileStorageTest()
        {
            ReadStreamTest(new FileStorage(""));
        }

        [TestMethod]
        public void ReadStreamWhenMemoryStorageTest()
        {
            ReadStreamTest(new MemoryStorage());
        }

        private void ReadStreamTest(IStorage storage)
        {
            // arrange
            var streamSize = 20480;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            using var memoryStream = new MemoryStream(randomlyBytes);
            Chunk = new Chunk(0, streamSize - 1) {
                Timeout = 100,
                Storage = storage
            };

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(memoryStream.Length, Chunk.Storage.GetLength());
            var chunkStream = Chunk.Storage.OpenRead();
            for (int i = 0; i < streamSize; i++)
            {
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());
            }

            Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsWhenMemoryStorageTest()
        {
            ReadStreamProgressEventsTest(new MemoryStorage());
        }

        [TestMethod]
        public void ReadStreamProgressEventsWhenFileStorageTest()
        {
            ReadStreamProgressEventsTest(new FileStorage(""));
        }

        private void ReadStreamProgressEventsTest(IStorage storage)
        {
            // arrange
            var eventCount = 0;
            var streamSize = 9 * Configuration.BufferBlockSize;
            using var memoryStream = new MemoryStream(new byte[streamSize]);
            Chunk = new Chunk(0, streamSize - 1) {
                Timeout = 100,
                Storage = storage
            };
            DownloadProgressChanged += delegate { eventCount++; };

            // act
            ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(streamSize/Configuration.BufferBlockSize, eventCount);

            Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamTimeoutExceptionTest()
        {
            // arrange
            var canceledToken = new CancellationToken(true);

            // act
            async Task CallReadStream() => await ReadStream(new MemoryStream(), canceledToken);

            // assert
            Assert.ThrowsExceptionAsync<OperationCanceledException>(CallReadStream);
        }
    }
}