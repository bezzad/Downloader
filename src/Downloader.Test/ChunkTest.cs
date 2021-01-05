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
            chunk.Storage.WriteAsync(new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 }, 0, 5).Wait();

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
                Storage = new MemoryStorage()
            };
            chunk.Storage.WriteAsync(new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 }, 0, 5).Wait();

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
                Storage = new MemoryStorage()
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
                Storage = new MemoryStorage(),
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
                Storage = new MemoryStorage(),
                Position = size-1
            };
            chunk.Storage.WriteAsync(DummyData.GenerateRandomBytes(size), 0, size).Wait();

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
            var dummyData = DummyData.GenerateRandomBytes(size);
            chunk.Storage.WriteAsync(dummyData, 0, size).Wait();

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
                Storage = new MemoryStorage()
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
                Storage = new MemoryStorage(),
                Position = size // overflowed
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWithEqualStorageSizeTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new MemoryStorage(),
                Position = 7
            };
            chunk.Storage.WriteAsync(new byte[] {0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7}, 0, 7);

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWithNoEqualStorageSizeTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new MemoryStorage(),
                Position = 10
            };
            chunk.Storage.WriteAsync(new byte[] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7 }, 0, 10);

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWhenNoStorageAndPositivePositionTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Position = 1
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWhenNoStorageAndZeroPositionTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Position = 0
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnZeroSizeTest()
        {
            // arrange
            var chunk = new Chunk(0, -1) {
                Storage = new MemoryStorage(),
                Position = 0
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void SetValidPositionOnOverflowTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size-1) {
                Storage = new MemoryStorage(),
                Position = size // overflowed
            };

            // act
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(0, chunk.Position);
        }

        [TestMethod]
        public void SetValidPositionWhenNoStorageAndPositivePositionTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Position = 1
            };

            // act
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(0, chunk.Position);
        }

        [TestMethod]
        public void SetValidPositionWithStorageAndPositivePositionTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) {
                Storage = new MemoryStorage(),
                Position = 1
            };

            // act
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(0, chunk.Position);
        }
    }
}
