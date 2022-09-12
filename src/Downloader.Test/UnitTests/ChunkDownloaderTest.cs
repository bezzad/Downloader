using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    public abstract class ChunkDownloaderTest
    {
        protected DownloadConfiguration _configuration;
        protected IStorage _storage;

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public void ReadStreamTest()
        {
            // arrange            
            var streamSize = DummyFileHelper.FileSize16Kb;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = _storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, new CancellationToken()).Wait();

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
        public void PauseResumeReadStreamTest()
        {
            // arrange            
            var streamSize = DummyFileHelper.FileSize16Kb;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = _storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);
            var pauseToken = new PauseTokenSource();
            var pauseCount = 0;

            // act
            chunkDownloader.DownloadProgressChanged += (sender, e) => {
                if (pauseCount < 10)
                {
                    pauseToken.Pause();
                    pauseCount++;
                    pauseToken.Resume();
                }
            };
            chunkDownloader.ReadStream(memoryStream, pauseToken.Token, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(memoryStream.Length, chunkDownloader.Chunk.Storage.GetLength());
            Assert.AreEqual(10, pauseCount);
            var chunkStream = chunkDownloader.Chunk.Storage.OpenRead();
            for (int i = 0; i < streamSize; i++)
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());

            chunkDownloader.Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsTest()
        {
            // arrange
            var eventCount = 0;
            var receivedBytes = new List<byte>();
            var streamSize = 9 * _configuration.BufferBlockSize;
            var source = DummyData.GenerateRandomBytes(streamSize);
            using var sourceMemoryStream = new MemoryStream(source);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = _storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            chunkDownloader.DownloadProgressChanged += (s, e) => {
                eventCount++;
                receivedBytes.AddRange(e.ReceivedBytes);
            };

            // act
            chunkDownloader.ReadStream(sourceMemoryStream, new PauseTokenSource().Token, new CancellationToken()).Wait();

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
            var streamSize = DummyFileHelper.FileSize16Kb;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 100, Storage = _storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);
            var canceledToken = new CancellationToken(true);

            // act
            async Task CallReadStream() => await chunkDownloader
                .ReadStream(new MemoryStream(), new PauseTokenSource().Token, canceledToken)
                .ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<OperationCanceledException>(CallReadStream);
        }

        [TestMethod]
        public async Task CancelReadStreamTest()
        {
            // arrange            
            var streamSize = DummyFileHelper.FileSize16Kb;
            var stoppedPosition = 0L;
            var randomlyBytes = DummyData.GenerateRandomBytes(streamSize);
            var cts = new CancellationTokenSource();
            var chunk = new Chunk(0, streamSize - 1) { Timeout = 1000, Storage = _storage };
            var chunkDownloader = new ChunkDownloader(chunk, _configuration);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            chunkDownloader.DownloadProgressChanged += (sender, e) => {
                if (e.ProgressPercentage > 50)
                {
                    stoppedPosition = e.ReceivedBytesSize;
                    cts.Cancel();
                }
            };
            async Task act() => await chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, cts.Token).ConfigureAwait(false);

            // assert
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(act);
            Assert.AreEqual(stoppedPosition, chunkDownloader.Chunk.Storage.GetLength());
            Assert.IsFalse(memoryStream.CanRead); // stream has been closed
            using var chunkStream = chunkDownloader.Chunk.Storage.OpenRead();
            for (int i = 0; i < stoppedPosition; i++)
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());

            chunkDownloader.Chunk.Clear();
        }
    }
}