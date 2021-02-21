using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public class FileStorageTest : StorageTest
    {
        [TestInitialize]
        public override void Initial()
        {
            Storage = new FileStorage("");
        }

        [TestMethod]
        public void SerializeFileStorageTest()
        {
            // arrange
            var data = DummyData.GenerateOrderedBytes(1024);
            Storage.WriteAsync(data, 0, data.Length).Wait();

            // act
            var serializedStorage = JsonConvert.SerializeObject(Storage);
            Storage.Close();
            var deserializedStorage = JsonConvert.DeserializeObject<FileStorage>(serializedStorage);

            // assert
            Assert.AreEqual(data.Length, deserializedStorage.GetLength());

            deserializedStorage.Clear();
        }

        [TestMethod]
        public void BinarySerializeFileStorageTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            var data = DummyData.GenerateOrderedBytes(1024);
            Storage.WriteAsync(data, 0, data.Length).Wait();
            using var binarySerializedStorage = new MemoryStream();

            // act
            formatter.Serialize(binarySerializedStorage, Storage);
            Storage.Close();
            binarySerializedStorage.Flush();
            binarySerializedStorage.Seek(0, SeekOrigin.Begin);
            var deserializedStorage = formatter.Deserialize(binarySerializedStorage) as FileStorage;

            // assert
            Assert.AreEqual(data.Length, deserializedStorage?.GetLength());
            deserializedStorage?.Clear();
        }
    }
}
