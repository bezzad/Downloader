using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Downloader.Test
{
    [TestClass]
    public class ChunkTest
    {
        [TestMethod]
        public void ClearTest()
        {
            // arrange
            var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 100 };
            chunk.CanTryAgainOnFailover();
            
            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Position);
            Assert.AreEqual(0, chunk.Timeout);
            Assert.AreEqual(0, chunk.FailoverCount);
        }

        [TestMethod]
        public void ClearFileStorageTest()
        {
            // arrange
            var chunk = new Chunk(0, 1000) {
                Storage = new FileStorage("")
            };
            chunk.Storage.Write(new byte[] {0x0, 0x1, 0x2, 0x3, 0x4}, 0, 5);

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Storage.GetLength());
        }

        [TestMethod]
        public void ClearMemoryStorageTest()
        {
            // arrange
            var chunk = new Chunk(0, 1000) {
                Storage = new MemoryStorage(5)
            };
            chunk.Storage.Write(new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 }, 0, 5);

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Storage.GetLength());
        }

        [TestMethod]
        public void IsDownloadCompletedOnBeginTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new MemoryStorage(size)
            };

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenNoStorageTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Position = size-1
            };
           
            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenFileStorageNoDataTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new FileStorage(""),
                Position = size-1
            };

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenMemoryStorageNoDataTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new MemoryStorage(size),
                Position = size-1
            };

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenMemoryStorageDataIsExistTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new MemoryStorage(size),
                Position = size-1
            };
            chunk.Storage.Write(DummyData.GenerateRandomBytes(size), 0, size);

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsDownloadCompletedWhenFileStorageDataIsExistTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new FileStorage(""),
                Position = size-1
            };
            chunk.Storage.Write(DummyData.GenerateRandomBytes(size), 0, size);

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionWithMemoryStorageTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new MemoryStorage(size)
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWithFileStorageTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new FileStorage("")
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnOverflowTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new MemoryStorage(size),
                Position = size // overflowed
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionTestWhenNoStorage()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size);

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }
    }
}
