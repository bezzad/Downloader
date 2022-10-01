using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public abstract class ChunkTest
    {
        private readonly byte[] _testData = DummyData.GenerateOrderedBytes(1024);
        protected IStorage Storage { get; set; }

        [TestInitialize]
        public abstract void InitialTest();

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
            Assert.AreEqual(0, chunk.FailoverCount);
        }

        [TestMethod]
        public void TestCanTryAgainOnFailoverWhenMaxIsZero()
        {
            // arrange
            var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 100, MaxTryAgainOnFailover = 0 };

            // act
            var canTryAgainOnFailover = chunk.CanTryAgainOnFailover();

            // assert
            Assert.IsFalse(canTryAgainOnFailover);
            Assert.AreEqual(1, chunk.FailoverCount);
        }

        [TestMethod]
        public void TestCanTryAgainOnFailoverWhenMaxIsOne()
        {
            // arrange
            var chunk = new Chunk(0, 1) { MaxTryAgainOnFailover = 1 };

            // act
            var canTryAgainOnFailover = chunk.CanTryAgainOnFailover();

            // assert
            Assert.IsTrue(canTryAgainOnFailover);
            Assert.AreEqual(1, chunk.FailoverCount);
        }

        [TestMethod]
        public async Task ClearStorageTest()
        {
            // arrange
            var chunk = new Chunk(0, 1000) { Storage = Storage };
            await chunk.Storage.WriteAsync(_testData, 0, 5, new CancellationToken()).ConfigureAwait(false);

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(0, chunk.Storage.GetLength());
        }

        [TestMethod]
        public void TestClearEffectLessOnTimeout()
        {
            // arrange
            var chunk = new Chunk(0, 1000) { Position = 100, Timeout = 1000 };

            // act
            chunk.Clear();

            // assert
            Assert.AreEqual(1000, chunk.Timeout);
        }

        [TestMethod]
        public void IsDownloadCompletedOnBeginTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) { Storage = Storage };

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
        public void IsDownloadCompletedWhenStorageNoDataTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) { Position = size - 1, Storage = Storage };

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public async Task IsDownloadCompletedWhenStorageDataIsExistTest()
        {
            // arrange
            var chunk = new Chunk(0, _testData.Length - 1) { Position = _testData.Length - 1, Storage = Storage };
            await chunk.Storage.WriteAsync(_testData, 0, _testData.Length, new CancellationToken()).ConfigureAwait(false);

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsTrue(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionWithStorageTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size) { Storage = Storage };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionOnOverflowTest()
        {
            // arrange
            var chunk = new Chunk(0, _testData.Length - 1) {
                Position = _testData.Length + 1,
                Storage = Storage // overflowed
            };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public async Task IsValidPositionWithEqualStorageSizeTest()
        {
            // arrange
            var chunk = new Chunk(0, _testData.Length - 1) {
                Position = 7,
                Storage = Storage
            };
            await chunk.Storage.WriteAsync(_testData, 0, 7, new CancellationToken()).ConfigureAwait(false);

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public async Task IsValidPositionWithNoEqualStorageSizeTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size - 1) {
                Position = 10,
                Storage = Storage
            };
            await chunk.Storage.WriteAsync(new byte[] { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7 }, 0, 6, new CancellationToken())
                .ConfigureAwait(false);

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsFalse(isValidPosition);
        }

        [TestMethod]
        public void IsValidPositionWhenNoStorageAndPositivePositionTest()
        {
            // arrange
            var chunk = new Chunk(0, 1024) {
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
            var chunk = new Chunk(0, 1024) {
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
            var chunk = new Chunk(0, -1) { Position = 0, Storage = Storage };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void SetValidPositionOnOverflowTest()
        {
            // arrange
            var chunk = new Chunk(0, 1023) {
                Position = 1024,
                Storage = Storage
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
            var chunk = new Chunk(0, 1024) {
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
            var chunk = new Chunk(0, 1024) { Position = 1, Storage = Storage };

            // act
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(0, chunk.Position);
        }

        [TestMethod]
        public async Task TestSetValidPositionWhenStorageChanged()
        {
            // arrange
            var nextPosition = 512;
            var chunk = new Chunk(0, 1024) {
                Position = 1,
                Storage = Storage
            };

            // act
            await Storage.WriteAsync(DummyData.GenerateRandomBytes(nextPosition), 0, nextPosition, new CancellationToken()).ConfigureAwait(false);
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(nextPosition, chunk.Position);
        }

        [TestMethod]
        public async Task ChunkSerializationTest()
        {
            // arrange
            var chunk = new Chunk(1024, 1024 + _testData.Length) {
                Position = 1,
                Timeout = 1000,
                MaxTryAgainOnFailover = 3000,
                Storage = Storage
            };
            await chunk.Storage.WriteAsync(_testData, 0, _testData.Length, new CancellationToken()).ConfigureAwait(false);

            // act
            var serializedChunk = JsonConvert.SerializeObject(chunk);
            var deserializedChunk = JsonConvert.DeserializeObject<Chunk>(serializedChunk);

            // assert
            AssertHelper.AreEquals(chunk, deserializedChunk);

            chunk.Clear();
        }

        [TestMethod]
        public async Task ChunkBinarySerializationTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            var chunk = new Chunk(1024, 1024 + _testData.Length) {
                Position = 1,
                Timeout = 1000,
                MaxTryAgainOnFailover = 3000,
                Storage = Storage
            };
            await chunk.Storage.WriteAsync(_testData, 0, _testData.Length, new CancellationToken()).ConfigureAwait(false);
            using var serializedChunk = new MemoryStream();

            // act
            formatter.Serialize(serializedChunk, chunk);
            serializedChunk.Flush();
            serializedChunk.Seek(0, SeekOrigin.Begin);
            var deserializedChunk = formatter.Deserialize(serializedChunk) as Chunk;

            // assert
            AssertHelper.AreEquals(chunk, deserializedChunk);

            chunk.Clear();
        }
    }
}
