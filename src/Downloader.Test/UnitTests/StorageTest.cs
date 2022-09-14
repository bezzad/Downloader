using Downloader.DummyHttpServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Downloader.Test.UnitTests
{
    public abstract class StorageTest
    {
        protected const int DataLength = 2048;
        protected readonly byte[] Data = DummyData.GenerateRandomBytes(DataLength);
        protected IStorage Storage { get; set; }

        [TestInitialize]
        public abstract void Initial();

        [TestMethod]
        public async Task OpenReadLengthTest()
        {
            // arrange
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);

            // act
            var reader = Storage.OpenRead();

            // assert
            Assert.AreEqual(DataLength, reader.Length);
        }

        [TestMethod]
        public async Task OpenReadStreamTest()
        {
            // arrange
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);

            // act
            var reader = Storage.OpenRead();

            // assert
            for (int i = 0; i < DataLength; i++)
            {
                Assert.AreEqual(Data[i], reader.ReadByte());
            }
        }

        [TestMethod]
        public async Task WriteAsyncLengthTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            await Storage.WriteAsync(Data, 0, length, new CancellationToken()).ConfigureAwait(false);

            // assert
            Assert.AreEqual(length, Storage.GetLength());
        }

        [TestMethod]
        public async Task WriteAsyncBytesTest()
        {
            // arrange
            var length = DataLength / 2;

            // act
            await Storage.WriteAsync(Data, 0, length, new CancellationToken()).ConfigureAwait(false);
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
            var writeCount = DataLength / count;

            // act
            for (int i = 0; i < count; i++)
            {
                await Storage.WriteAsync(Data, writeCount * i, writeCount, new CancellationToken()).ConfigureAwait(false);
            }

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
            var offset = 1;

            // act
            async Task WriteMethod() => 
                await Storage.WriteAsync(Data, offset, DataLength, new CancellationToken())
                        .ConfigureAwait(false);

            // assert
            Assert.ThrowsExceptionAsync<ArgumentException>(WriteMethod);
        }

        [TestMethod]
        public async Task ClearTest()
        {
            // arrange
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);

            // act
            Storage.Clear();

            // assert
            Assert.AreEqual(0, Storage.GetLength());
        }

        [TestMethod]
        public async Task FlushTest()
        {
            // arrange
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);

            // act
            Storage.Flush();

            // assert
            Assert.AreEqual(Data.Length, Storage.GetLength());
        }

        [TestMethod]
        public async Task GetLengthTest()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            await Storage.WriteAsync(data, 0, 1, new CancellationToken()).ConfigureAwait(false);

            // act
            var actualLength = Storage.GetLength();

            // assert
            Assert.AreEqual(1, actualLength);
        }

        [TestMethod]
        public async Task TestStreamExpandability()
        {
            // arrange
            var data = new byte[] { 0x0, 0x1, 0x2, 0x3, 0x4 };
            await Storage.WriteAsync(data, 0, data.Length, new CancellationToken()).ConfigureAwait(false);
            Storage.Flush();

            // act
            var serializedStream = JsonConvert.SerializeObject(Storage);
            var mutableStream = JsonConvert.DeserializeObject(serializedStream, Storage.GetType()) as IStorage;
            await mutableStream.WriteAsync(data, 0, data.Length, new CancellationToken()).ConfigureAwait(false);

            // assert
            Assert.AreEqual(data.Length*2, mutableStream?.GetLength());

            Storage.Clear();
        }

        [TestMethod]
        public async Task TestWriteStorageWhenCanceled()
        {
            // arrange
            var canceledToken = new CancellationToken(true);

            // act
            async Task act()=> await Storage.WriteAsync(Data, 0, DataLength, canceledToken).ConfigureAwait(false);
            var reader = Storage.OpenRead();

            // assert
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(act);
        }
    }
}