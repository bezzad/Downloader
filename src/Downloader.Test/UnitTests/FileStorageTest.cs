using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
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
        public async Task SerializeFileStorageTest()
        {
            // arrange
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);

            // act
            var serializedStorage = JsonConvert.SerializeObject(Storage);
            Storage.Close();
            var deserializedStorage = JsonConvert.DeserializeObject<FileStorage>(serializedStorage);

            // assert
            Assert.AreEqual(DataLength, deserializedStorage.GetLength());

            deserializedStorage.Clear();
        }

        [TestMethod]
        public async Task BinarySerializeFileStorageTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            await Storage.WriteAsync(Data, 0, DataLength, new CancellationToken()).ConfigureAwait(false);
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
