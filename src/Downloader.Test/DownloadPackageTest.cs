using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Downloader.Test
{
    [TestClass]
    public abstract class DownloadPackageTest
    {
        protected DownloadConfiguration Configuration { get; set; }
        private DownloadPackage Package { get; set; }

        [TestInitialize]
        public virtual void Initial()
        {
            var testData = DummyData.GenerateOrderedBytes(DownloadTestHelper.FileSize16Kb);
            Package = new DownloadPackage() {
                FileName = DownloadTestHelper.File16KbName,
                Address = DownloadTestHelper.File16KbUrl,
                Chunks = new ChunkHub(Configuration).ChunkFile(DownloadTestHelper.FileSize16Kb, 8),
                TotalFileSize = DownloadTestHelper.FileSize16Kb
            };

            foreach (var chunk in Package.Chunks)
            {
                chunk.Storage.WriteAsync(testData, (int)chunk.Start, (int)chunk.Length);
                Package.AddReceivedBytes(chunk.Length);
            }
        }

        [TestMethod]
        public void PackageSerializationTest()
        {
            // arrange
            var settings = new Newtonsoft.Json.JsonSerializerSettings();
            settings.Converters.Add(new StorageConverter());

            // act
            var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(Package);
            var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<DownloadPackage>(serialized, settings);

            // assert
            PackagesAreEqual(Package, deserialized);

            Package.Clear();
        }

        [TestMethod]
        public void PackageBinarySerializationTest()
        {
            // arrange
            IFormatter formatter = new BinaryFormatter();
            using var serializedStream = new MemoryStream();

            // act
            formatter.Serialize(serializedStream, Package);
            serializedStream.Flush();
            serializedStream.Seek(0, SeekOrigin.Begin);
            var deserialized = formatter.Deserialize(serializedStream) as DownloadPackage;

            // assert
            PackagesAreEqual(Package, deserialized);

            Package.Clear();
        }

        private void PackagesAreEqual(DownloadPackage source, DownloadPackage destination)
        {
            Assert.IsNotNull(source);
            Assert.IsNotNull(destination);
            Assert.IsNotNull(source.Chunks);
            Assert.IsNotNull(destination.Chunks);
            Assert.AreEqual(source.FileName, destination.FileName);
            Assert.AreEqual(source.ReceivedBytesSize, destination.ReceivedBytesSize);
            Assert.AreEqual(source.Address, destination.Address);
            Assert.AreEqual(source.TotalFileSize, destination.TotalFileSize);
            Assert.AreEqual(source.Chunks?.Length, destination.Chunks?.Length);

            for (int i = 0; i < source.Chunks.Length; i++)
            {
                AssertHelper.AreEquals(source.Chunks[i], destination.Chunks[i]);
            }
        }

        [TestMethod]
        public void ClearChunksTest()
        {
            // act
            Package.Clear();

            // assert
            Assert.IsNull(Package.Chunks);
        }

        [TestMethod]
        public void ClearPackageTest()
        {
            // arrange
            Package.ReceivedBytesSize = 1000;

            // act
            Package.Clear();

            // assert
            Assert.AreEqual(0, Package.ReceivedBytesSize);
        }
    }
}
