using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace Downloader.Test.UnitTests
{
    [TestClass]
    public class ConcurrentStreamTest
    {
        [TestMethod]
        public void TestInitialSizeOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 1024 * 1024; // 1MB

            // act
            var stream = new ConcurrentStream(path, size);

            // assert
            Assert.AreEqual(size, new FileInfo(path).Length);

            // clean up
            stream.Dispose();
            File.Delete(path);
        }

        [TestMethod]
        public void TestWriteOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 1024; // 1KB
            var data = new byte[] { 1 };

            // act
            var stream = new ConcurrentStream(path, size);
            for (int i = 0; i < size; i++)
                stream.WriteAsync(i, data, 1);

            stream.Flush();

            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size, new FileInfo(path).Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(1, readerStream.ReadByte());

            // clean up
            stream.Dispose();
            File.Delete(path);
        }

        [TestMethod]
        public void TestOverflowOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 1024; // 1KB
            var data = new byte[] { 1 };

            // act
            var stream = new ConcurrentStream(path, size / 2);
            for (int i = 0; i < size; i++)
                stream.WriteAsync(i, data, 1);

            stream.Flush();

            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size, new FileInfo(path).Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(1, readerStream.ReadByte());

            // clean up
            stream.Dispose();
            File.Delete(path);
        }

        [TestMethod]
        public void TestAccessMoreThanSizeOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 10;
            var jumpStepCount = 1024; // 1KB
            var data = new byte[] { 1, 1, 1, 1, 1 };
            var selectedDataLen = 3;

            // act
            var stream = new ConcurrentStream(path, size);
            stream.WriteAsync(size + jumpStepCount, data, selectedDataLen);
            stream.Flush();
            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size + jumpStepCount + selectedDataLen, new FileInfo(path).Length);
            for (int i = 0; i < size + jumpStepCount; i++)
                Assert.AreEqual(0, readerStream.ReadByte()); // empty spaces

            for (int i = 0; i < selectedDataLen; i++)
                Assert.AreEqual(1, readerStream.ReadByte()); // wrote data spaces

            Assert.AreEqual(-1, readerStream.ReadByte()); // end of stream

            // clean up
            stream.Dispose();
            File.Delete(path);
        }

        [TestMethod]
        public void TestDynamicBufferDataOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 1024; // 1KB
            var data = new byte[10]; // zero bytes
            var stream = new ConcurrentStream(path, size);

            // act
            for (int i = 0; i < size / 8; i++)
            {
                stream.WriteAsync(i * 8, data, 8);
                data.Fill((byte)(i + 1));
            }
            stream.Flush();
            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size, new FileInfo(path).Length);
            data = new byte[8];
            for (int i = 0; i < size / 8; i++)
            {
                data.Fill((byte)i);
                var buffer = new byte[8];
                Assert.AreEqual(8, readerStream.Read(buffer, 0, 8));
                Assert.IsTrue(buffer.SequenceEqual(data));
            }

            Assert.AreEqual(-1, readerStream.ReadByte()); // end of stream

            // clean up
            stream.Dispose();
            File.Delete(path);
        }


        [TestMethod]
        public void TestSerializationOnFileStream()
        {
            // arrange
            var path = Path.GetTempFileName();
            var size = 256;
            var data = DummyData.GenerateOrderedBytes(size);
            var stream = new ConcurrentStream(path, size);

            // act
            stream.WriteAsync(0, data, size);
            var serializedStream = JsonConvert.SerializeObject(stream);
            stream.Dispose();
            var newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
            var readerStream = newStream.OpenRead();

            // assert
            Assert.AreEqual(size, readerStream.Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(i, readerStream.ReadByte());

            // clean up
            newStream.Dispose();
            File.Delete(path);
        }

        [TestMethod]
        public void TestInitialSizeOnMemoryStream()
        {
            // act
            var stream = new ConcurrentStream();

            // assert
            Assert.IsInstanceOfType(stream.OpenRead(), typeof(MemoryStream));

            // clean up
            stream.Dispose();
        }

        [TestMethod]
        public void TestWriteOnMemoryStream()
        {
            // arrange
            var size = 1024; // 1KB
            var data = new byte[] { 1 };

            // act
            var stream = new ConcurrentStream();
            for (int i = 0; i < size; i++)
                stream.WriteAsync(i, data, 1);

            stream.Flush();

            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size, readerStream.Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(1, readerStream.ReadByte());

            // clean up
            stream.Dispose();
        }

        [TestMethod]
        public void TestDynamicBufferDataOnMemoryStream()
        {
            // arrange
            var size = 1024; // 1KB
            var data = new byte[10]; // zero bytes
            var stream = new ConcurrentStream();

            // act
            for (int i = 0; i < size / 8; i++)
            {
                stream.WriteAsync(i * 8, data, 8);
                data.Fill((byte)(i + 1));
            }
            stream.Flush();
            var readerStream = stream.OpenRead();

            // assert
            Assert.AreEqual(size, readerStream.Length);
            data = new byte[8];
            for (int i = 0; i < size / 8; i++)
            {
                data.Fill((byte)i);
                var buffer = new byte[8];
                Assert.AreEqual(8, readerStream.Read(buffer, 0, 8));
                Assert.IsTrue(buffer.SequenceEqual(data));
            }

            Assert.AreEqual(-1, readerStream.ReadByte()); // end of stream

            // clean up
            stream.Dispose();
        }

        [TestMethod]
        public void TestSerializationOnMemoryStream()
        {
            // arrange
            var size = 256;
            var data = DummyData.GenerateOrderedBytes(size);
            var stream = new ConcurrentStream();

            // act
            stream.WriteAsync(0, data, size);
            var serializedStream = JsonConvert.SerializeObject(stream);
            stream.Dispose();
            var newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
            var readerStream = newStream.OpenRead();

            // assert
            Assert.AreEqual(size, readerStream.Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(i, readerStream.ReadByte());

            // clean up
            newStream.Dispose();
        }
    }
}