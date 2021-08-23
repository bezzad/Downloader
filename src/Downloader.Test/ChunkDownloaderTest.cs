using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkDownloaderTest
    {
        private DownloadConfiguration _configuration;

        [TestInitialize]
        public void Initial()
        {
            _configuration = new DownloadConfiguration {
                BufferBlockSize = 1024,
                ChunkCount = 16,
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
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            chunkDownloader.ReadStream(memoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(memoryStream.Length, chunkDownloader.Chunk.Storage.GetLength());
            var chunkStream = chunkDownloader.Chunk.Storage.OpenRead();
            for (int i = 0; i < streamSize; i++)
            {
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());
            }

            chunkDownloader.Chunk.Clear();
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
            var receivedBytes = new List<byte>();
            var streamSize = 9 * _configuration.BufferBlockSize;
            var source = DummyData.GenerateRandomBytes(streamSize);
            using var sourceMemoryStream = new MemoryStream(source);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            chunkDownloader.DownloadProgressChanged += (s, e) => {
                eventCount++;
                receivedBytes.AddRange(e.ReceivedBytes);
            };

            // act
            chunkDownloader.ReadStream(sourceMemoryStream, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(streamSize/_configuration.BufferBlockSize, eventCount);
            Assert.AreEqual(chunkDownloader.Chunk.Length, receivedBytes.Count);
            Assert.IsTrue(source.SequenceEqual(receivedBytes));

            chunkDownloader.Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamTimeoutExceptionTest()
        {
            // arrange
            var streamSize = 20480;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100 };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);
            var canceledToken = new CancellationToken(true);

            // act
            async Task CallReadStream() => await chunkDownloader.ReadStream(new MemoryStream(), canceledToken).ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<OperationCanceledException>(CallReadStream);
        }
    }
}