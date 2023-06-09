using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace Downloader.Test.HelperTests
{
    [TestClass]
    public class DummyLazyStreamTest
    {
        [TestMethod]
        public void GenerateOrderedBytesStreamTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();
            var memBuffer = new MemoryStream();

            // act
            var dummyData = new DummyLazyStream(DummyDataType.Order, size).ToArray();

            // assert
            Assert.AreEqual(size, dummyData.Length);
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
        }

        [TestMethod]
        public void GenerateOrderedBytesStreamLessThan1Test()
        {
            // arrange
            int size = 0;

            // act
            void act() => new DummyLazyStream(DummyDataType.Order, size);

            // assert
            Assert.ThrowsException<ArgumentException>(act);
        }

        [TestMethod]
        public void GenerateRandomBytesStreamTest()
        {
            // arrange
            int size = 1024;

            // act
            var dummyData = new DummyLazyStream(DummyDataType.Random, size).ToArray();

            // assert
            Assert.AreEqual(size, dummyData.Length);
            Assert.IsTrue(dummyData.Any(i => i > 0));
        }

        [TestMethod]
        public void GenerateRandomBytesLessThan1Test()
        {
            // arrange
            int size = 0;

            // act
            void act() => new DummyLazyStream(DummyDataType.Random, size);

            // assert
            Assert.ThrowsException<ArgumentException>(act);
        }

        [TestMethod]
        public void GenerateSingleBytesTest()
        {
            // arrange
            int size = 1024;
            byte fillByte = 13;

            // act
            var dummyData = new DummyLazyStream(DummyDataType.Single, size, fillByte).ToArray();

            // assert
            Assert.AreEqual(size, dummyData.Length);
            Assert.IsTrue(dummyData.All(i => i == fillByte));
        }
    }
}
