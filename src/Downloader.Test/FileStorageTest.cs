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
    }
}
