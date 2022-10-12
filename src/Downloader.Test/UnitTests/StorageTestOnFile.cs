using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Downloader.Test.UnitTests
{
    public class StorageTestOnFile : StorageTest
    {
        private string path;
        private int size;
        private ConcurrentStream _storage;
        protected override ConcurrentStream Storage => _storage ??= new ConcurrentStream(path, size);

        [TestInitialize]
        public override void Initial()
        {
            size = 1024 * 1024; // 1MB
            path = Path.GetTempFileName();
        }

        [TestCleanup]
        public override void Cleanup()
        {
            base.Cleanup();
            File.Delete(path);
        }

        [TestMethod]
        public void TestInitialSizeOnFileStream()
        {
            // act
            var Storage = new ConcurrentStream(path, size);

            // assert
            Assert.AreEqual(size, new FileInfo(path).Length);
            Assert.AreEqual(size, Storage.Length);
        }

        [TestMethod]
        public void TestInitialSizeWithNegativeNumberOnFileStream()
        {
            // arrange
            size = -1;

            // act
            Storage.Flush(); // create lazy stream

            // assert
            Assert.AreEqual(0, new FileInfo(path).Length);
            Assert.AreEqual(0, Storage.Length);
        }

        [TestMethod]
        public void TestWriteSizeOverflowOnFileStream()
        {
            // arrange
            size = 512;
            var actualSize = size*2;
            var data = new byte[] { 1 };

            // act
            for (int i = 0; i < actualSize; i++)
                Storage.WriteAsync(i, data, 1);

            Storage.Flush();
            var readerStream = Storage.OpenRead();

            // assert
            Assert.AreEqual(actualSize, new FileInfo(path).Length);
            Assert.AreEqual(actualSize, Storage.Length);
            for (int i = 0; i < actualSize; i++)
                Assert.AreEqual(1, readerStream.ReadByte());
        }

        [TestMethod]
        public void TestAccessMoreThanSizeOnFileStream()
        {
            // arrange
            size = 10;
            var jumpStepCount = 1024; // 1KB
            var data = new byte[] { 1, 1, 1, 1, 1 };
            var selectedDataLen = 3;
            var actualSize = size + jumpStepCount + selectedDataLen;

            // act
            Storage.WriteAsync(size + jumpStepCount, data, selectedDataLen);
            Storage.Flush();
            var readerStream = Storage.OpenRead();

            // assert
            Assert.AreEqual(actualSize, new FileInfo(path).Length);
            Assert.AreEqual(actualSize, Storage.Length);
            for (int i = 0; i < size + jumpStepCount; i++)
                Assert.AreEqual(0, readerStream.ReadByte()); // empty spaces

            for (int i = 0; i < selectedDataLen; i++)
                Assert.AreEqual(1, readerStream.ReadByte()); // wrote data spaces

            Assert.AreEqual(-1, readerStream.ReadByte()); // end of stream
        }
    }
}
