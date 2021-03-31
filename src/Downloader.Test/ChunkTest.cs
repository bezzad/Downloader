using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
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
        public void ClearStorageTest()
        {
            // arrange
            var chunk = new Chunk(0, 1000) { Storage = Storage };
            chunk.Storage.WriteAsync(_testData, 0, 5).Wait();

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
        public void IsDownloadCompletedWhenStorageDataIsExistTest()
        {
            // arrange
            var chunk = new Chunk(0, _testData.Length - 1) { Position = _testData.Length - 1, Storage = Storage };
            chunk.Storage.WriteAsync(_testData, 0, _testData.Length).Wait();

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
        public void IsValidPositionWithEqualStorageSizeTest()
        {
            // arrange
            var chunk = new Chunk(0, _testData.Length - 1) {
                Position = 7,
                Storage = Storage
            };
            chunk.Storage.WriteAsync(_testData, 0, 7);

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
            var chunk = new Chunk(0, size - 1) {
                Position = 10,
                Storage = Storage
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
        public void TestSetValidPositionWhenStorageChanged()
        {
            // arrange
            var nextPosition = 512;
            var chunk = new Chunk(0, 1024) {
                Position = 1,
                Storage = Storage
            };

            // act
            Storage.WriteAsync(DummyData.GenerateRandomBytes(nextPosition), 0, nextPosition);
            chunk.SetValidPosition();

            // assert
            Assert.AreEqual(nextPosition, chunk.Position);
        }

        [TestMethod]
        public void ChunkSerializationTest()
        {
            // arrange
            var settings = new JsonSerializerSettings();
            settings.Converters.Add(new StorageConverter());
            var chunk = new Chunk(1024, 1024 + _testData.Length) {
                Position = 1,
                Timeout = 1000,
                MaxTryAgainOnFailover = 3000,
                Storage = Storage
            };
            chunk.Storage.WriteAsync(_testData, 0, _testData.Length).Wait();

            // act
            var serializedChunk = JsonConvert.SerializeObject(chunk);
            var deserializedChunk = JsonConvert.DeserializeObject<Chunk>(serializedChunk, settings);

            // assert
            AssertHelper.AreEquals(chunk, deserializedChunk);

            chunk.Clear();
        }

        [TestMethod]
        public void ChunkBinarySerializationTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            var chunk = new Chunk(1024, 1024 + _testData.Length) {
                Position = 1,
                Timeout = 1000,
                MaxTryAgainOnFailover = 3000,
                Storage = Storage
            };
            chunk.Storage.WriteAsync(_testData, 0, _testData.Length).Wait();
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
