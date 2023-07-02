using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    public abstract class StorageTest
    {
        protected const int DataLength = 2048;
        protected readonly byte[] Data = DummyData.GenerateRandomBytes(DataLength);
        protected virtual ConcurrentStream Storage { get; }

        [TestInitialize]
        public virtual void Initial()
        {
            // write pre-requirements of each tests
        }

        [TestCleanup]
        public virtual void Cleanup()
        {
            Storage?.Dispose();
        }

        [TestMethod]
        public async Task OpenReadLengthTest()
        {
            // arrange
            await Storage.WriteAsync(0, Data, DataLength);
            Storage.Flush();

            // act
            var reader = Storage.OpenRead();

            // assert
            Assert.AreEqual(DataLength, reader.Length);
        }

        [TestMethod]
        public async Task OpenReadStreamTest()
        {
            // arrange
            await Storage.WriteAsync(0, Data, DataLength);
            Storage.Flush();

            // act
            var reader = Storage.OpenRead();

            // assert
            for (int i = 0; i < DataLength; i++)
            {
                Assert.AreEqual(Data[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public async Task SlowWriteTest()
        {
            // arrange
            var data = new byte[] { 1 };
            var size = 1024;

            // act
            for (int i = 0; i < size; i++)
                await Storage.WriteAsync(i, data, 1);

            Storage.Flush();
            var readerStream = Storage.OpenRead();

            // assert
            Assert.AreEqual(size, Storage.Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(1, readerStream.ReadByte());
        }

        [TestMethod]
        public async Task WriteAsyncLengthTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            await Storage.WriteAsync(0, Data, length);
            Storage.Flush();

            // assert
            Assert.AreEqual(length, Storage.Length);
        }

        [TestMethod]
        public async Task WriteAsyncBytesTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            await Storage.WriteAsync(0, Data, length);
            Storage.Flush();
            var reader = Storage.OpenRead();

            // assert
            for (int i = 0; i < length; i++)
            {
                Assert.AreEqual(Data[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public async Task WriteAsyncMultipleTimeTest()
        {
            // arrange
            var count = 128;
            var size = DataLength / count;

            // act
            for (int i = 0; i < count; i++)
            {
                var startOffset = i * size;
                await Storage.WriteAsync(startOffset, Data.Skip(startOffset).Take(size).ToArray(), size);
            }
            Storage.Flush();

            // assert
            var reader = Storage.OpenRead();
            for (int i = 0; i < DataLength; i++)
            {
                Assert.AreEqual(Data[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public void WriteAsyncOutOfRangeExceptionTest()
        {
            // arrange
            var length = DataLength + 1;

            // act
            void WriteMethod() => Storage.WriteAsync(0, Data, length).Wait();

            // assert
            Assert.ThrowsException<ArgumentException>(WriteMethod);
        }

        [TestMethod]
        public async Task TestDispose()
        {
            // arrange
            await Storage.WriteAsync(0, Data, DataLength);

            // act
            Storage.Dispose();

            // assert
            Assert.AreEqual(0, Storage.Length);
        }

        [TestMethod]
        public async Task FlushTest()
        {
            // arrange
            await Storage.WriteAsync(0, Data, DataLength);

            // act
            Storage.Flush();

            // assert
            Assert.AreEqual(Data.Length, Storage.Length);
        }

        [TestMethod]
        public async Task GetLengthTest()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            await Storage.WriteAsync(0, data, 1);
            Storage.Flush();

            // act
            var actualLength = Storage.Length;

            // assert
            Assert.AreEqual(1, actualLength);
        }

        [TestMethod]
        public async Task TestStreamExpandability()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            await Storage.WriteAsync(0, data, data.Length);
            Storage.Flush();

            // act
            var serializedStream = JsonConvert.SerializeObject(Storage);
            var mutableStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
            await mutableStream.WriteAsync(0, data, data.Length);
            mutableStream.Flush();

            // assert
            Assert.AreEqual(data.Length * 2, mutableStream?.Length);
        }

        [TestMethod]
        public async Task TestDynamicBufferData()
        {
            // arrange
            var size = 1024; // 1KB

            // act
            for (int i = 0; i < size / 8; i++)
            {
                var data = new byte[10]; // zero bytes
                Array.Fill(data, (byte)i);
                await Storage.WriteAsync(i * 8, data, 8);
            }
            Storage.Flush();
            var readerStream = Storage.OpenRead();

            // assert
            Assert.AreEqual(size, Storage.Length);
            for (int i = 0; i < size / 8; i++)
            {
                var data = new byte[8]; // zero bytes
                Array.Fill(data, (byte)i);
                var buffer = new byte[8];
                Assert.AreEqual(8, readerStream.Read(buffer, 0, 8));
                Assert.IsTrue(buffer.SequenceEqual(data));
            }

            Assert.AreEqual(-1, readerStream.ReadByte()); // end of stream
        }

        [TestMethod]
        public async Task TestSerialization()
        {
            // arrange
            var size = 256;
            var data = DummyData.GenerateOrderedBytes(size);

            // act
            await Storage.WriteAsync(0, data, size);
            var serializedStream = JsonConvert.SerializeObject(Storage);
            Storage.Dispose();
            var newStream = JsonConvert.DeserializeObject<ConcurrentStream>(serializedStream);
            var readerStream = newStream.OpenRead();

            // assert
            Assert.AreEqual(size, readerStream.Length);
            for (int i = 0; i < size; i++)
                Assert.AreEqual(i, readerStream.ReadByte());
        }
    }
}