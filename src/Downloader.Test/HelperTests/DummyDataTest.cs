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
            Assert.IsTrue(dummyData.SequenceEqual(bytes));
            Assert.AreEqual(size, dummyData.Length);
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
            Assert.IsTrue(dummyData.Any(i => i > 0));
            Assert.AreEqual(size, dummyData.Length);
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
    }
}
