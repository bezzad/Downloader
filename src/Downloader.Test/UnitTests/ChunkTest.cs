using Downloader.DummyHttpServer;
using Downloader.Test.Helper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ChunkTest
    {
        private readonly byte[] _testData = DummyData.GenerateOrderedBytes(1024);

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
            var chunk = new Chunk(0, size);

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
                Position = size - 1
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
            var chunk = new Chunk(0, size) { Position = size - 1 };

            // act
            bool isDownloadCompleted = chunk.IsDownloadCompleted();

            // assert
            Assert.IsFalse(isDownloadCompleted);
        }

        [TestMethod]
        public void IsValidPositionWithStorageTest()
        {
            // arrange
            var size = 1024;
            var chunk = new Chunk(0, size);

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
            };

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
            var chunk = new Chunk(0, -1) { Position = 0 };

            // act
            bool isValidPosition = chunk.IsValidPosition();

            // assert
            Assert.IsTrue(isValidPosition);
        }

        [TestMethod]
        public void ChunkSerializationTest()
        {
            // arrange
            var chunk = new Chunk(1024, 1024 + _testData.Length) {
                Position = 1,
                Timeout = 1000,
                MaxTryAgainOnFailover = 3000,
            };

            // act
            var serializedChunk = JsonConvert.SerializeObject(chunk);
            var deserializedChunk = JsonConvert.DeserializeObject<Chunk>(serializedChunk);

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
                MaxTryAgainOnFailover = 3000
            };
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
