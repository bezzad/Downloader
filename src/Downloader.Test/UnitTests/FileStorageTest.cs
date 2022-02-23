using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test.UnitTests
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
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();

            // act
            var serializedStorage = JsonConvert.SerializeObject(Storage);
            Storage.Close();
            var deserializedStorage = JsonConvert.DeserializeObject<FileStorage>(serializedStorage);

            // assert
            Assert.AreEqual(DataLength, deserializedStorage.GetLength());

            deserializedStorage.Clear();
        }

        [TestMethod]
        public void BinarySerializeFileStorageTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            Storage.WriteAsync(DummyData, 0, DataLength).Wait();
            using var binarySerializedStorage = new MemoryStream();

            // act
            formatter.Serialize(binarySerializedStorage, Storage);
            Storage.Close();
            binarySerializedStorage.Flush();
            binarySerializedStorage.Seek(0, SeekOrigin.Begin);
            var deserializedStorage = formatter.Deserialize(binarySerializedStorage) as FileStorage;

            // assert
            Assert.AreEqual(DataLength, deserializedStorage?.GetLength());
            deserializedStorage?.Clear();
        }
    }
}
