using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkDownloaderTest : ChunkDownloader
    {
        public ChunkDownloaderTest()
            : base(new MemoryChunk(0, 10000), 1024)
        {
        }

        public ChunkDownloaderTest(Chunk chunk, int blockSize)
            : base(chunk, blockSize)
        {
        }
        
        private Exception GetException()
        {
            try
            {
                throw new Exception("High level exception",
                    new IOException("Mid level exception", new HttpRequestException("Low level exception")));
            }
            catch (Exception e)
            {
                return e;
            }
        }

        [TestMethod]
        public void HasSourceFromThisNamespaceTest()
        {
            // arrange
            Exception exception = GetException();

            // act
            bool hasThisNamespace = HasSource(exception, GetType().Namespace);

            // assert
            Assert.IsTrue(hasThisNamespace);
        }

        [TestMethod]
        public void HasSourceFromNonOccurrenceNamespaceTest()
        {
            // arrange
            Exception exception = GetException();

            // act
            bool hasSocketsNamespace = HasSource(exception, "System.Net.Sockets");
            bool hasSecurityNamespace = HasSource(exception, "System.Net.Security");

            // assert
            Assert.IsFalse(hasSocketsNamespace);
            Assert.IsFalse(hasSecurityNamespace);
        }

        [TestMethod]
        public void IsDownloadCompletedTest()
        {
            // arrange
            Chunk.Position = unchecked((int)(Chunk.End - Chunk.Start));

            // act
            bool isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedOnBeginTest()
        {
            // arrange
            Chunk.Position = 0;

            // act
            bool isDownloadCompleted = IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionTest()
        {
            // arrange
            Chunk.Position = 0;

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnOverflowTest()
        {
            // arrange
            Chunk.Position = unchecked((int)(Chunk.End - Chunk.Start)) + 1;

            // act
            bool isValidPosition = IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        protected override void CreateChunkStorage()
        {
            throw new NotImplementedException();
        }

        protected override Task WriteChunk(byte[] data, int count)
        {
            throw new NotImplementedException();
        }
    }
}