using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Downloader.Test.HelperTests
{
    [TestClass]
    public class DummyDataTest
    {
        [TestMethod]
        public void GenerateOrderedBytesTest()
        {
            // arrange
            int size = 1024;
            byte[] bytes = Enumerable.Range(0, size).Select(i => (byte)i).ToArray();

            // act
            var dummyData = DummyData.GenerateOrderedBytes(size);

            // assert
            Assert.AreEqual(size, dummyData.Length);
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
        }

        [TestMethod]
        public void GenerateOrderedBytesLessThan1Test()
        {
            // arrange
            int size = 0;

            // act
            void act() => DummyData.GenerateOrderedBytes(size);

            // assert
            Assert.ThrowsException<ArgumentException>(act);
        }

        [TestMethod]
        public void GenerateRandomBytesTest()
        {
            // arrange
            int size = 1024;

            // act
            var dummyData = DummyData.GenerateRandomBytes(size);

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
            void act() => DummyData.GenerateRandomBytes(size);

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
            var dummyData = DummyData.GenerateSingleBytes(size, fillByte);

            // assert
            Assert.AreEqual(size, dummyData.Length);
            Assert.IsTrue(dummyData.All(i => i == fillByte));
        }
    }
}
