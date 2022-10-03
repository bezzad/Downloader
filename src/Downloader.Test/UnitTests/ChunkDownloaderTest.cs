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
        protected DownloadConfiguration Configuration { get; set; }
        protected ConcurrentStream Storage { get; set; }
        protected int Size { get; set; } = DummyFileHelper.FileSize16Kb;

        [TestInitialize]
        public abstract void InitialTest();

        [TestMethod]
        public void ReadStreamTest()
        {
            // arrange
            var randomlyBytes = DummyData.GenerateRandomBytes(Size);
            var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            chunkDownloader.ReadStream(memoryStream, new PauseTokenSource().Token, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(memoryStream.Length, Storage.Length);
            var chunkStream = Storage.OpenRead();
            for (int i = 0; i < Size; i++)
            {
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());
            }

            chunkDownloader.Chunk.Clear();
        }

        [TestMethod]
        public void PauseResumeReadStreamTest()
        {
            // arrange            
            var randomlyBytes = DummyData.GenerateRandomBytes(Size);
            var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
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
            Assert.AreEqual(memoryStream.Length, Storage.Length);
            Assert.AreEqual(10, pauseCount);
            var chunkStream = Storage.OpenRead();
            for (int i = 0; i < Size; i++)
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());

            chunkDownloader.Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamProgressEventsTest()
        {
            // arrange
            var eventCount = 0;
            var receivedBytes = new List<byte>();
            var source = DummyData.GenerateRandomBytes(Size);
            using var sourceMemoryStream = new MemoryStream(source);
            var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
            chunkDownloader.DownloadProgressChanged += (s, e) => {
                eventCount++;
                receivedBytes.AddRange(e.ReceivedBytes);
            };

            // act
            chunkDownloader.ReadStream(sourceMemoryStream, new PauseTokenSource().Token, new CancellationToken()).Wait();

            // assert
            Assert.AreEqual(Size / Configuration.BufferBlockSize, eventCount);
            Assert.AreEqual(chunkDownloader.Chunk.Length, receivedBytes.Count);
            Assert.IsTrue(source.SequenceEqual(receivedBytes));

            chunkDownloader.Chunk.Clear();
        }

        [TestMethod]
        public void ReadStreamCanceledExceptionTest()
        {
            // arrange
            var randomlyBytes = DummyData.GenerateRandomBytes(Size);
            var chunk = new Chunk(0, Size - 1) { Timeout = 100 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
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
        public void ReadStreamTimeoutExceptionTest()
        {
            // arrange
            var randomlyBytes = DummyData.GenerateRandomBytes(Size);
            var chunk = new Chunk(0, Size - 1) { Timeout = 0 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
            using var memoryStream = new MemoryStream(randomlyBytes);

            // act
            async Task CallReadStream() => await chunkDownloader
                .ReadStream(new MemoryStream(), new PauseTokenSource().Token, new CancellationToken())
                .ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<TaskCanceledException>(CallReadStream);
        }

        [TestMethod]
        public async Task CancelReadStreamTest()
        {
            // arrange 
            var stoppedPosition = 0L;
            var randomlyBytes = DummyData.GenerateRandomBytes(Size);
            var cts = new CancellationTokenSource();
            var chunk = new Chunk(0, Size - 1) { Timeout = 1000 };
            var chunkDownloader = new ChunkDownloader(chunk, Configuration, Storage);
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
            await Assert.ThrowsExceptionAsync<OperationCanceledException>(act).ConfigureAwait(false);
            Assert.AreEqual(stoppedPosition, Storage.Length);
            Assert.IsFalse(memoryStream.CanRead); // stream has been closed
            using var chunkStream = Storage.OpenRead();
            for (int i = 0; i < stoppedPosition; i++)
                Assert.AreEqual(randomlyBytes[i], chunkStream.ReadByte());

            chunkDownloader.Chunk.Clear();
        }
    }
}